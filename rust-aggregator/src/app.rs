use crate::aggregation::{aggregate_all_row_groups, merge_row_group_results};
use crate::cli::resolve_input_path;
use crate::formatting::{format_bytes, format_count, format_elapsed, print_results};
use crate::parquet_io::column_indexes;
use crate::AppResult;
use parquet::file::reader::{FileReader, SerializedFileReader};
use std::fs::File;
use std::time::Instant;

pub fn run() -> AppResult<()> {
    let path = resolve_input_path()?;
    if !path.exists() {
        return Err(format!("Parquet file not found: {}", path.display()).into());
    }

    let file_size = path.metadata()?.len();
    println!("Reading {}", path.display());
    println!("File size: {}", format_bytes(file_size));

    let stopwatch = Instant::now();
    let reader = SerializedFileReader::new(File::open(&path)?)?;
    let indexes = column_indexes(reader.metadata().file_metadata().schema_descr())?;
    let row_group_count = reader.num_row_groups();

    let results = aggregate_all_row_groups(&path, row_group_count, indexes)?;
    let summary = merge_row_group_results(results, row_group_count, &stopwatch);

    for progress in summary.progress {
        println!(
            "Row group {}/{}: processed {} rows in {}",
            progress.row_group_index + 1,
            row_group_count,
            format_count(progress.rows_read),
            format_elapsed(progress.elapsed)
        );
    }

    let elapsed = stopwatch.elapsed();
    print_results(
        &summary.orders_by_region,
        &summary.revenue_by_region,
        summary.rows_read,
        elapsed.as_secs_f64(),
    );

    Ok(())
}
