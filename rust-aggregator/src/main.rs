mod aggregation;
mod app;
mod cli;
mod constants;
mod formatting;
mod parquet_io;

use std::error::Error;

pub type AppResult<T> = Result<T, Box<dyn Error + Send + Sync>>;

fn main() {
    if let Err(error) = app::run() {
        eprintln!("{error}");
        std::process::exit(1);
    }
}
