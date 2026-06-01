#include <algorithm>
#include <array>
#include <chrono>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <filesystem>
#include <iomanip>
#include <iostream>
#include <memory>
#include <mutex>
#include <sstream>
#include <stdexcept>
#include <string>
#include <string_view>
#include <thread>
#include <atomic>
#include <vector>

#include <arrow/api.h>
#include <arrow/io/api.h>
#include <parquet/arrow/reader.h>

namespace {

constexpr std::string_view kDefaultFileName = "orders.parquet";
constexpr std::string_view kDefaultDataDirectory = "data";
constexpr std::array<std::string_view, 6> kRegionNames{
    "North", "South", "East", "West", "Central", "Online"};

struct CliOptions {
    std::filesystem::path path;
};

struct Aggregation {
    std::array<std::uint64_t, kRegionNames.size()> orders_by_region{};
    std::array<double, kRegionNames.size()> revenue_by_region{};
    std::uint64_t rows_read = 0;
};

struct RowGroupResult {
    Aggregation aggregation;
    std::chrono::steady_clock::duration elapsed{};
};

void PrintUsage() {
    std::cout << "C++ Parquet Aggregator\n\n"
              << "Usage:\n"
              << "  cpp-aggregator.exe\n"
              << "  cpp-aggregator.exe --path C:\\path\\to\\orders.parquet\n\n"
              << "When --path is omitted, the app reads .\\data\\orders.parquet from the repository root.\n";
}

[[noreturn]] void ThrowStatus(const arrow::Status& status, std::string_view context) {
    throw std::runtime_error(std::string(context) + ": " + status.ToString());
}

template <typename T>
T ValueOrThrow(arrow::Result<T> result, std::string_view context) {
    if (!result.ok()) {
        ThrowStatus(result.status(), context);
    }

    return std::move(result).ValueUnsafe();
}

void EnsureOk(const arrow::Status& status, std::string_view context) {
    if (!status.ok()) {
        ThrowStatus(status, context);
    }
}

std::filesystem::path ResolveRepositoryRoot() {
    auto current = std::filesystem::current_path();
    for (auto candidate = current; !candidate.empty(); candidate = candidate.parent_path()) {
        if (std::filesystem::is_directory(candidate / "dotnet-app") &&
            std::filesystem::is_directory(candidate / "rust-aggregator")) {
            return candidate;
        }

        if (candidate == candidate.root_path()) {
            break;
        }
    }

    return current;
}

CliOptions ParseOptions(int argc, char** argv) {
    if (argc == 1) {
        return CliOptions{ResolveRepositoryRoot() /
                          std::filesystem::path(kDefaultDataDirectory) /
                          std::filesystem::path(kDefaultFileName)};
    }

    std::string_view first = argv[1];
    if (first == "-h" || first == "--help" || first == "help") {
        PrintUsage();
        std::exit(0);
    }

    if (first != "--path") {
        throw std::runtime_error("Unknown argument: " + std::string(first));
    }

    if (argc < 3) {
        throw std::runtime_error("Missing value for --path");
    }

    if (argc > 3) {
        throw std::runtime_error("Unexpected extra arguments");
    }

    return CliOptions{std::filesystem::path(argv[2])};
}

std::string FormatCount(std::uint64_t value) {
    std::string digits = std::to_string(value);
    std::string output;
    output.reserve(digits.size() + digits.size() / 3);

    std::size_t remaining = digits.size();
    for (char ch : digits) {
        output.push_back(ch);
        --remaining;
        if (remaining > 0 && remaining % 3 == 0) {
            output.push_back(',');
        }
    }

    return output;
}

std::string FormatNumberWithDecimals(double value, int decimals) {
    std::ostringstream stream;
    stream << std::fixed << std::setprecision(decimals) << value;
    std::string text = stream.str();

    std::string whole = text;
    std::string fraction;
    if (auto dot = text.find('.'); dot != std::string::npos) {
        whole = text.substr(0, dot);
        fraction = text.substr(dot + 1);
    }

    std::uint64_t whole_number = 0;
    if (!whole.empty() && whole != "-") {
        whole_number = static_cast<std::uint64_t>(std::stoull(whole));
    }

    if (fraction.empty()) {
        return FormatCount(whole_number);
    }

    return FormatCount(whole_number) + "." + fraction;
}

std::string FormatCurrency(double value) {
    return "$" + FormatNumberWithDecimals(value, 2);
}

std::string FormatBytes(std::uintmax_t bytes) {
    constexpr std::array<const char*, 5> units{"B", "KB", "MB", "GB", "TB"};
    double value = static_cast<double>(bytes);
    std::size_t unit = 0;

    while (value >= 1024.0 && unit + 1 < units.size()) {
        value /= 1024.0;
        ++unit;
    }

    std::ostringstream stream;
    stream << std::fixed << std::setprecision(2) << value << ' ' << units[unit];
    return stream.str();
}

template <typename Duration>
std::string FormatElapsed(Duration elapsed) {
    using namespace std::chrono;

    auto total_seconds = duration_cast<seconds>(elapsed).count();
    auto hours = total_seconds / 3600;
    auto minutes = (total_seconds % 3600) / 60;
    auto seconds_only = total_seconds % 60;
    auto hundred_ns = duration_cast<nanoseconds>(elapsed).count() / 100;
    auto fraction = static_cast<std::uint64_t>(hundred_ns % 10'000'000);

    std::ostringstream stream;
    stream << std::setfill('0') << std::setw(2) << hours << ':' << std::setw(2) << minutes << ':'
           << std::setw(2) << seconds_only << '.' << std::setw(7) << fraction;
    return stream.str();
}

void PrintResults(const Aggregation& aggregation, double elapsed_seconds, std::chrono::steady_clock::duration elapsed) {
    std::cout << "\nAggregation by region\n";
    std::cout << "Region       Orders            Revenue\n";
    std::cout << "----------------------------------------------\n";

    std::uint64_t total_orders = 0;
    double total_revenue = 0.0;
    for (std::size_t i = 0; i < kRegionNames.size(); ++i) {
        total_orders += aggregation.orders_by_region[i];
        total_revenue += aggregation.revenue_by_region[i];

        std::ostringstream line;
        line << std::left << std::setw(10) << kRegionNames[i] << std::right << std::setw(14)
             << FormatCount(aggregation.orders_by_region[i]) << std::setw(18)
             << FormatCurrency(aggregation.revenue_by_region[i]);
        std::cout << line.str() << '\n';
    }

    std::ostringstream total_line;
    total_line << std::left << std::setw(10) << "Total" << std::right << std::setw(14)
               << FormatCount(total_orders) << std::setw(18) << FormatCurrency(total_revenue);

    std::cout << "----------------------------------------------\n";
    std::cout << total_line.str() << "\n\n";
    std::cout << "Processed " << FormatCount(aggregation.rows_read) << " rows in " << FormatElapsed(elapsed)
              << '\n';

    const auto throughput = static_cast<std::uint64_t>(
        std::llround(static_cast<double>(aggregation.rows_read) / std::max(elapsed_seconds, 0.001)));
    std::cout << "Throughput: " << FormatCount(throughput) << " rows/sec\n";
}

int FindColumnIndex(const std::shared_ptr<arrow::Schema>& schema, std::string_view name) {
    int index = schema->GetFieldIndex(std::string(name));
    if (index < 0) {
        throw std::runtime_error("Column not found: " + std::string(name));
    }

    return index;
}

template <typename ArrowArrayType, typename NativeType>
std::vector<NativeType> ReadChunkedValues(const std::shared_ptr<arrow::ChunkedArray>& values,
                                          arrow::Type::type expected_type,
                                          std::string_view column_name) {
    if (values->type()->id() != expected_type) {
        throw std::runtime_error("Unexpected type for column " + std::string(column_name));
    }

    std::vector<NativeType> output;
    output.reserve(static_cast<std::size_t>(values->length()));

    for (const auto& chunk : values->chunks()) {
        if (chunk->null_count() != 0) {
            throw std::runtime_error("Column contains nulls: " + std::string(column_name));
        }

        auto typed = std::static_pointer_cast<ArrowArrayType>(chunk);
        for (int64_t i = 0; i < typed->length(); ++i) {
            output.push_back(typed->Value(i));
        }
    }

    return output;
}

Aggregation AggregateRowGroup(parquet::arrow::FileReader* reader,
                              int row_group,
                              const std::vector<int>& columns) {
    auto table = ValueOrThrow(reader->ReadRowGroup(row_group, columns), "Failed to read row group");

    auto quantities_chunked = table->column(0);
    auto unit_prices_chunked = table->column(1);
    auto regions_chunked = table->column(2);

    Aggregation aggregation;

    auto quantities = ReadChunkedValues<arrow::Int32Array, std::int32_t>(
        quantities_chunked, arrow::Type::INT32, "Quantity");
    auto unit_prices = ReadChunkedValues<arrow::DoubleArray, double>(
        unit_prices_chunked, arrow::Type::DOUBLE, "UnitPrice");

    const auto row_count = quantities.size();
    if (unit_prices.size() != row_count || static_cast<std::size_t>(regions_chunked->length()) != row_count) {
        throw std::runtime_error("Row group has mismatched column lengths");
    }

    if (regions_chunked->type()->id() == arrow::Type::UINT8) {
        auto region_ids = ReadChunkedValues<arrow::UInt8Array, std::uint8_t>(
            regions_chunked, arrow::Type::UINT8, "RegionId");
        for (std::size_t i = 0; i < row_count; ++i) {
            const auto region_index = static_cast<std::size_t>(region_ids[i] % kRegionNames.size());
            aggregation.orders_by_region[region_index] += 1;
            aggregation.revenue_by_region[region_index] +=
                static_cast<double>(quantities[i]) * unit_prices[i];
        }
    } else if (regions_chunked->type()->id() == arrow::Type::INT32) {
        auto region_ids = ReadChunkedValues<arrow::Int32Array, std::int32_t>(
            regions_chunked, arrow::Type::INT32, "RegionId");
        for (std::size_t i = 0; i < row_count; ++i) {
            const auto region_index = static_cast<std::size_t>(
                static_cast<std::uint32_t>(region_ids[i]) % kRegionNames.size());
            aggregation.orders_by_region[region_index] += 1;
            aggregation.revenue_by_region[region_index] +=
                static_cast<double>(quantities[i]) * unit_prices[i];
        }
    } else if (regions_chunked->type()->id() == arrow::Type::INT64) {
        auto region_ids = ReadChunkedValues<arrow::Int64Array, std::int64_t>(
            regions_chunked, arrow::Type::INT64, "RegionId");
        for (std::size_t i = 0; i < row_count; ++i) {
            const auto region_index = static_cast<std::size_t>(
                static_cast<std::uint64_t>(region_ids[i]) % kRegionNames.size());
            aggregation.orders_by_region[region_index] += 1;
            aggregation.revenue_by_region[region_index] +=
                static_cast<double>(quantities[i]) * unit_prices[i];
        }
    } else {
        throw std::runtime_error("Unsupported RegionId type");
    }

    aggregation.rows_read = static_cast<std::uint64_t>(row_count);
    return aggregation;
}

void MergeAggregation(Aggregation& total, const Aggregation& row_group) {
    total.rows_read += row_group.rows_read;
    for (std::size_t i = 0; i < kRegionNames.size(); ++i) {
        total.orders_by_region[i] += row_group.orders_by_region[i];
        total.revenue_by_region[i] += row_group.revenue_by_region[i];
    }
}

std::vector<RowGroupResult> AggregateAllRowGroupsInParallel(
    const std::string& path,
    int row_group_count,
    int quantity_index,
    int unit_price_index,
    int region_id_index,
    const std::chrono::steady_clock::time_point started) {
    std::vector<RowGroupResult> results(static_cast<std::size_t>(row_group_count));
    std::atomic<int> next_row_group{0};
    std::atomic<bool> has_error{false};
    std::mutex error_mutex;
    std::string error_message;

    const unsigned int suggested = std::max(1u, std::thread::hardware_concurrency());
    const int worker_count = std::max(1, std::min(row_group_count, static_cast<int>(suggested)));

    std::vector<std::thread> workers;
    workers.reserve(static_cast<std::size_t>(worker_count));
    for (int worker_id = 0; worker_id < worker_count; ++worker_id) {
        (void)worker_id;
        workers.emplace_back([&]() {
            try {
                auto input = ValueOrThrow(arrow::io::ReadableFile::Open(path), "Failed to open file");
                auto reader = ValueOrThrow(
                    parquet::arrow::OpenFile(input, arrow::default_memory_pool()),
                    "Failed to create Parquet reader");
                const std::vector<int> columns{quantity_index, unit_price_index, region_id_index};

                while (!has_error.load(std::memory_order_relaxed)) {
                    const int row_group = next_row_group.fetch_add(1, std::memory_order_relaxed);
                    if (row_group >= row_group_count) {
                        break;
                    }

                    RowGroupResult result;
                    result.aggregation = AggregateRowGroup(reader.get(), row_group, columns);
                    result.elapsed = std::chrono::steady_clock::now() - started;
                    results[static_cast<std::size_t>(row_group)] = std::move(result);
                }
            } catch (const std::exception& ex) {
                has_error.store(true, std::memory_order_relaxed);
                std::lock_guard<std::mutex> lock(error_mutex);
                if (error_message.empty()) {
                    error_message = ex.what();
                }
            }
        });
    }

    for (auto& worker : workers) {
        worker.join();
    }

    if (!error_message.empty()) {
        throw std::runtime_error(error_message);
    }

    return results;
}

}  // namespace

int main(int argc, char** argv) {
    try {
        const CliOptions options = ParseOptions(argc, argv);
        const auto full_path = std::filesystem::absolute(options.path);

        if (!std::filesystem::exists(full_path)) {
            std::cerr << "Parquet file not found: " << full_path.string() << '\n';
            return 1;
        }

        std::cout << "Reading " << full_path.string() << '\n';
        std::cout << "File size: " << FormatBytes(std::filesystem::file_size(full_path)) << '\n';

        auto input = ValueOrThrow(arrow::io::ReadableFile::Open(full_path.string()), "Failed to open file");

        auto reader = ValueOrThrow(
            parquet::arrow::OpenFile(input, arrow::default_memory_pool()),
            "Failed to create Parquet reader");

        std::shared_ptr<arrow::Schema> schema;
        EnsureOk(reader->GetSchema(&schema), "Failed to read Parquet schema");

        const int quantity_index = FindColumnIndex(schema, "Quantity");
        const int unit_price_index = FindColumnIndex(schema, "UnitPrice");
        const int region_id_index = FindColumnIndex(schema, "RegionId");

        const int row_group_count = reader->num_row_groups();

        const auto started = std::chrono::steady_clock::now();
        auto row_group_results = AggregateAllRowGroupsInParallel(
            full_path.string(),
            row_group_count,
            quantity_index,
            unit_price_index,
            region_id_index,
            started);

        Aggregation aggregation;
        auto last_elapsed = std::chrono::steady_clock::duration::zero();
        for (int row_group = 0; row_group < row_group_count; ++row_group) {
            const auto& result = row_group_results[static_cast<std::size_t>(row_group)];
            MergeAggregation(aggregation, result.aggregation);
            last_elapsed = std::max(last_elapsed, result.elapsed);
            std::cout << "Row group " << FormatCount(static_cast<std::uint64_t>(row_group + 1)) << '/'
                      << FormatCount(static_cast<std::uint64_t>(row_group_count)) << ": processed "
                      << FormatCount(aggregation.rows_read) << " rows in " << FormatElapsed(last_elapsed)
                      << '\n';
        }

        const auto elapsed = std::chrono::steady_clock::now() - started;
        const auto elapsed_seconds = std::chrono::duration<double>(elapsed).count();
        PrintResults(aggregation, elapsed_seconds, elapsed);

        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
