use crate::AppResult;
use parquet::basic::Type as PhysicalType;
use parquet::column::reader::ColumnReader;
use parquet::file::reader::RowGroupReader;
use parquet::schema::types::SchemaDescriptor;

#[derive(Clone, Copy)]
pub struct ColumnIndexes {
    pub quantity: usize,
    pub unit_price: usize,
    pub region_id: usize,
}

pub enum RegionValues {
    Int32(Vec<i32>),
    Int64(Vec<i64>),
}

pub fn column_indexes(schema: &SchemaDescriptor) -> AppResult<ColumnIndexes> {
    Ok(ColumnIndexes {
        quantity: find_column_index(schema, "Quantity")?,
        unit_price: find_column_index(schema, "UnitPrice")?,
        region_id: find_column_index(schema, "RegionId")?,
    })
}

pub fn read_i32_column(
    row_group: &dyn RowGroupReader,
    column_index: usize,
    row_count: usize,
) -> AppResult<Vec<i32>> {
    let mut column = row_group.get_column_reader(column_index)?;
    match &mut column {
        ColumnReader::Int32ColumnReader(reader) => {
            let mut values = Vec::with_capacity(row_count);
            let (records_read, values_read, _levels_read) =
                reader.read_records(row_count, None, None, &mut values)?;
            ensure_full_column("i32", row_count, records_read, values_read)?;
            Ok(values)
        }
        _ => Err(format!("Expected INT32 column at index {column_index}").into()),
    }
}

pub fn read_f64_column(
    row_group: &dyn RowGroupReader,
    column_index: usize,
    row_count: usize,
) -> AppResult<Vec<f64>> {
    let mut column = row_group.get_column_reader(column_index)?;
    match &mut column {
        ColumnReader::DoubleColumnReader(reader) => {
            let mut values = Vec::with_capacity(row_count);
            let (records_read, values_read, _levels_read) =
                reader.read_records(row_count, None, None, &mut values)?;
            ensure_full_column("double", row_count, records_read, values_read)?;
            Ok(values)
        }
        _ => Err(format!("Expected DOUBLE column at index {column_index}").into()),
    }
}

pub fn read_region_column(
    row_group: &dyn RowGroupReader,
    column_index: usize,
    row_count: usize,
) -> AppResult<RegionValues> {
    let physical_type = row_group.metadata().column(column_index).column_type();

    match physical_type {
        PhysicalType::INT32 => read_i32_column(row_group, column_index, row_count)
            .map(RegionValues::Int32),
        PhysicalType::INT64 => read_i64_column(row_group, column_index, row_count)
            .map(RegionValues::Int64),
        other => Err(format!("Unsupported RegionId physical type: {other:?}").into()),
    }
}

fn read_i64_column(
    row_group: &dyn RowGroupReader,
    column_index: usize,
    row_count: usize,
) -> AppResult<Vec<i64>> {
    let mut column = row_group.get_column_reader(column_index)?;
    match &mut column {
        ColumnReader::Int64ColumnReader(reader) => {
            let mut values = Vec::with_capacity(row_count);
            let (records_read, values_read, _levels_read) =
                reader.read_records(row_count, None, None, &mut values)?;
            ensure_full_column("i64", row_count, records_read, values_read)?;
            Ok(values)
        }
        _ => Err(format!("Expected INT64 column at index {column_index}").into()),
    }
}

fn find_column_index(schema: &SchemaDescriptor, name: &str) -> AppResult<usize> {
    schema
        .columns()
        .iter()
        .position(|column| column.name() == name)
        .ok_or_else(|| format!("Column not found: {name}").into())
}

fn ensure_full_column(
    column_type: &str,
    expected: usize,
    records_read: usize,
    values_read: usize,
) -> AppResult<()> {
    if records_read == expected && values_read == expected {
        return Ok(());
    }

    Err(format!(
        "Expected {expected} {column_type} records but read {records_read} records and {values_read} values"
    )
    .into())
}
