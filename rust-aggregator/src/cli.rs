use crate::constants::DEFAULT_FILE_NAME;
use crate::AppResult;
use std::env;
use std::path::{Path, PathBuf};

pub fn resolve_input_path() -> AppResult<PathBuf> {
    let mut args = env::args().skip(1);
    let Some(first) = args.next() else {
        return repository_root().map(|directory| directory.join("data").join(DEFAULT_FILE_NAME));
    };

    match first.as_str() {
        "-h" | "--help" | "help" => {
            print_usage();
            std::process::exit(0);
        }
        "--path" => {
            let Some(path) = args.next() else {
                return Err("Missing value for --path".into());
            };
            Ok(PathBuf::from(path))
        }
        other => Err(format!("Unknown argument: {other}").into()),
    }
}

fn executable_directory() -> AppResult<PathBuf> {
    let exe = env::current_exe()?;
    Ok(exe
        .parent()
        .unwrap_or_else(|| Path::new("."))
        .to_path_buf())
}

fn repository_root() -> AppResult<PathBuf> {
    for start in [env::current_dir()?, executable_directory()?] {
        for directory in start.ancestors() {
            if directory.join("dotnet-app").is_dir() && directory.join("rust-aggregator").is_dir() {
                return Ok(directory.to_path_buf());
            }
        }
    }

    Ok(env::current_dir()?)
}

fn print_usage() {
    println!(
        r#"Rust Parquet Aggregator

Usage:
  rust-aggregator.exe
  rust-aggregator.exe --path C:\path\to\orders.parquet

When --path is omitted, the app reads .\data\orders.parquet from the repository root.
"#
    );
}
