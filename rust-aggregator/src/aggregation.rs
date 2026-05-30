use crate::constants::REGION_NAMES;
use crate::parquet_io::{read_f64_column, read_i32_column, read_region_column, ColumnIndexes, RegionValues};
use crate::AppResult;
use parquet::file::reader::{FileReader, SerializedFileReader};
use rayon::prelude::*;
use std::fs::File;
use std::path::Path;
use std::time::{Duration, Instant};

pub fn aggregate_all_row_groups(
    path: &Path,
    row_group_count: usize,
    indexes: ColumnIndexes,
) -> AppResult<Vec<RowGroupAggregation>> {
    let mut results = (0..row_group_count)
        .into_par_iter()
        .map(|row_group_index| aggregate_row_group(path, row_group_index, indexes))
        .collect::<AppResult<Vec<_>>>()?;

    results.sort_by_key(|result| result.row_group_index);
    Ok(results)
}

pub fn merge_row_group_results(
    results: Vec<RowGroupAggregation>,
    _row_group_count: usize,
    stopwatch: &Instant,
) -> AggregationSummary {
    let mut revenue_by_region = [0.0_f64; REGION_NAMES.len()];
    let mut orders_by_region = [0_u64; REGION_NAMES.len()];
    let mut rows_read = 0_u64;
    let mut progress = Vec::with_capacity(results.len());

    for result in results {
        rows_read += result.row_count as u64;
        for i in 0..REGION_NAMES.len() {
            orders_by_region[i] += result.orders_by_region[i];
            revenue_by_region[i] += result.revenue_by_region[i];
        }

        progress.push(RowGroupProgress {
            row_group_index: result.row_group_index,
            rows_read,
            elapsed: stopwatch.elapsed(),
        });
    }

    AggregationSummary {
        orders_by_region,
        revenue_by_region,
        rows_read,
        progress,
    }
}

fn aggregate_row_group(
    path: &Path,
    row_group_index: usize,
    indexes: ColumnIndexes,
) -> AppResult<RowGroupAggregation> {
    let reader = SerializedFileReader::new(File::open(path)?)?;
    let row_group = reader.get_row_group(row_group_index)?;
    let row_count = row_group.metadata().num_rows() as usize;

    let quantities = read_i32_column(&*row_group, indexes.quantity, row_count)?;
    let unit_prices = read_f64_column(&*row_group, indexes.unit_price, row_count)?;
    let region_ids = read_region_column(&*row_group, indexes.region_id, row_count)?;

    let mut revenue_by_region = [0.0_f64; REGION_NAMES.len()];
    let mut orders_by_region = [0_u64; REGION_NAMES.len()];

    match region_ids {
        RegionValues::Int32(values) => {
            for i in 0..row_count {
                let region = values[i] as usize % REGION_NAMES.len();
                orders_by_region[region] += 1;
                revenue_by_region[region] += quantities[i] as f64 * unit_prices[i];
            }
        }
        RegionValues::Int64(values) => {
            for i in 0..row_count {
                let region = values[i] as usize % REGION_NAMES.len();
                orders_by_region[region] += 1;
                revenue_by_region[region] += quantities[i] as f64 * unit_prices[i];
            }
        }
    }

    Ok(RowGroupAggregation {
        row_group_index,
        row_count,
        orders_by_region,
        revenue_by_region,
    })
}

pub struct AggregationSummary {
    pub orders_by_region: [u64; REGION_NAMES.len()],
    pub revenue_by_region: [f64; REGION_NAMES.len()],
    pub rows_read: u64,
    pub progress: Vec<RowGroupProgress>,
}

pub struct RowGroupProgress {
    pub row_group_index: usize,
    pub rows_read: u64,
    pub elapsed: Duration,
}

pub struct RowGroupAggregation {
    pub row_group_index: usize,
    pub row_count: usize,
    pub orders_by_region: [u64; REGION_NAMES.len()],
    pub revenue_by_region: [f64; REGION_NAMES.len()],
}
