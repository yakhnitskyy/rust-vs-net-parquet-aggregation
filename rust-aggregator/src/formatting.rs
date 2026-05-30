use crate::constants::REGION_NAMES;
use std::time::Duration;

pub fn print_results(
    orders_by_region: &[u64; REGION_NAMES.len()],
    revenue_by_region: &[f64; REGION_NAMES.len()],
    rows_read: u64,
    elapsed_seconds: f64,
) {
    println!();
    println!("Aggregation by region");
    println!("Region       Orders            Revenue");
    println!("----------------------------------------------");

    let mut total_orders = 0_u64;
    let mut total_revenue = 0.0_f64;
    for i in 0..REGION_NAMES.len() {
        total_orders += orders_by_region[i];
        total_revenue += revenue_by_region[i];
        println!(
            "{:<10} {:>14} {:>18}",
            REGION_NAMES[i],
            format_count(orders_by_region[i]),
            format_currency(revenue_by_region[i])
        );
    }

    println!("----------------------------------------------");
    println!(
        "{:<10} {:>14} {:>18}",
        "Total",
        format_count(total_orders),
        format_currency(total_revenue)
    );
    println!();
    println!(
        "Processed {} rows in {}",
        format_count(rows_read),
        format_elapsed(Duration::from_secs_f64(elapsed_seconds))
    );

    let throughput = rows_read as f64 / elapsed_seconds.max(0.001);
    println!("Throughput: {} rows/sec", format_count(throughput.round() as u64));
}

pub fn format_bytes(bytes: u64) -> String {
    let units = ["B", "KB", "MB", "GB", "TB"];
    let mut value = bytes as f64;
    let mut unit = 0;
    while value >= 1024.0 && unit < units.len() - 1 {
        value /= 1024.0;
        unit += 1;
    }

    format!("{value:.2} {}", units[unit])
}

pub fn format_count(value: u64) -> String {
    let digits = value.to_string();
    let mut output = String::with_capacity(digits.len() + digits.len() / 3);
    for (index, ch) in digits.chars().rev().enumerate() {
        if index > 0 && index % 3 == 0 {
            output.push(',');
        }
        output.push(ch);
    }

    output.chars().rev().collect()
}

pub fn format_elapsed(duration: Duration) -> String {
    let total_seconds = duration.as_secs();
    let hours = total_seconds / 3600;
    let minutes = (total_seconds % 3600) / 60;
    let seconds = total_seconds % 60;
    let fraction = duration.subsec_nanos() / 100;

    format!("{hours:02}:{minutes:02}:{seconds:02}.{fraction:07}")
}

fn format_currency(value: f64) -> String {
    format!("${}", format_number_with_decimals(value, 2))
}

fn format_number_with_decimals(value: f64, decimals: usize) -> String {
    let formatted = format!("{value:.decimals$}");
    let (whole, fraction) = formatted
        .split_once('.')
        .unwrap_or((formatted.as_str(), ""));
    let whole_number = whole.parse::<u64>().unwrap_or(0);

    if fraction.is_empty() {
        format_count(whole_number)
    } else {
        format!("{}.{}", format_count(whole_number), fraction)
    }
}
