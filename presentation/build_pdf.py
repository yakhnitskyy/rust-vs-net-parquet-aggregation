from __future__ import annotations

from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.pagesizes import landscape
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import inch
from reportlab.pdfbase.pdfmetrics import stringWidth
from reportlab.pdfgen.canvas import Canvas
from reportlab.platypus import Paragraph, Table, TableStyle


ROOT = Path(__file__).resolve().parent
OUTPUT = ROOT / "parquet-performance-findings.pdf"

PAGE_WIDTH = 13.333 * inch
PAGE_HEIGHT = 7.5 * inch
MARGIN_X = 0.66 * inch
TOP = PAGE_HEIGHT - 0.56 * inch
BOTTOM = 0.45 * inch

INK = colors.HexColor("#101624")
TEXT = colors.HexColor("#465267")
MUTED = colors.HexColor("#6b7688")
TEAL = colors.HexColor("#0f6b7a")
BLUE = colors.HexColor("#173b63")
LINE = colors.HexColor("#d9e0ea")
PANEL = colors.HexColor("#ffffff")
PALE = colors.HexColor("#eef7f7")
WARN = colors.HexColor("#fff5e6")
WARN_LINE = colors.HexColor("#d99a2b")


styles = getSampleStyleSheet()
styles.add(
    ParagraphStyle(
        name="Kicker",
        parent=styles["Normal"],
        fontName="Helvetica-Bold",
        fontSize=8.5,
        leading=10,
        textColor=MUTED,
        uppercase=True,
    )
)
styles.add(
    ParagraphStyle(
        name="DeckTitle",
        parent=styles["Normal"],
        fontName="Helvetica-Bold",
        fontSize=35,
        leading=38,
        textColor=INK,
        spaceAfter=10,
    )
)
styles.add(
    ParagraphStyle(
        name="Lead",
        parent=styles["Normal"],
        fontName="Helvetica",
        fontSize=14.5,
        leading=20,
        textColor=colors.HexColor("#344156"),
    )
)
styles.add(
    ParagraphStyle(
        name="Body",
        parent=styles["Normal"],
        fontName="Helvetica",
        fontSize=10.7,
        leading=15,
        textColor=TEXT,
    )
)
styles.add(
    ParagraphStyle(
        name="CardBody",
        parent=styles["Normal"],
        fontName="Helvetica",
        fontSize=9.6,
        leading=12.2,
        textColor=TEXT,
    )
)
styles.add(
    ParagraphStyle(
        name="Small",
        parent=styles["Normal"],
        fontName="Helvetica",
        fontSize=8.3,
        leading=11,
        textColor=MUTED,
    )
)
styles.add(
    ParagraphStyle(
        name="CardTitle",
        parent=styles["Normal"],
        fontName="Helvetica-Bold",
        fontSize=18,
        leading=21,
        textColor=INK,
    )
)
styles.add(
    ParagraphStyle(
        name="Label",
        parent=styles["Normal"],
        fontName="Helvetica-Bold",
        fontSize=8.5,
        leading=10,
        textColor=MUTED,
    )
)


def p(canvas: Canvas, text: str, style: str, x: float, y_top: float, width: float, height: float | None = None) -> float:
    para = Paragraph(text, styles[style])
    required_width, required_height = para.wrap(width, height or PAGE_HEIGHT)
    para.drawOn(canvas, x, y_top - required_height)
    return required_height


def rounded_rect(canvas: Canvas, x: float, y: float, width: float, height: float, fill, stroke=LINE, radius: float = 8):
    canvas.setFillColor(fill)
    canvas.setStrokeColor(stroke)
    canvas.setLineWidth(0.75)
    canvas.roundRect(x, y, width, height, radius, stroke=1, fill=1)


def page_background(canvas: Canvas):
    canvas.setFillColor(colors.HexColor("#f6f8fb"))
    canvas.rect(0, 0, PAGE_WIDTH, PAGE_HEIGHT, stroke=0, fill=1)
    canvas.setFillColor(colors.HexColor("#ffffff"))
    canvas.setFillAlpha(0.72)
    canvas.circle(PAGE_WIDTH - 0.7 * inch, PAGE_HEIGHT - 0.55 * inch, 1.35 * inch, stroke=0, fill=1)
    canvas.setFillAlpha(1)


def footer(canvas: Canvas, page: int, source: str):
    y = 0.23 * inch
    p(canvas, source, "Small", MARGIN_X, y + 0.18 * inch, 8.95 * inch)
    canvas.setFont("Helvetica-Bold", 8.5)
    canvas.setFillColor(MUTED)
    canvas.drawRightString(PAGE_WIDTH - MARGIN_X, y + 0.07 * inch, f"{page:02d} / 03")


def header(canvas: Canvas, kicker: str, title: str, lead: str):
    p(canvas, kicker.upper(), "Kicker", MARGIN_X, TOP, 6 * inch)
    title_h = p(canvas, title, "DeckTitle", MARGIN_X, TOP - 0.18 * inch, 8.8 * inch)
    p(canvas, lead, "Lead", MARGIN_X, TOP - 0.23 * inch - title_h, 9.4 * inch)


def fact_row(canvas: Canvas, x: float, y_top: float, width: float, label: str, body: str) -> float:
    label_w = 1.0 * inch
    body_x = x + label_w + 0.16 * inch
    p(canvas, label.upper(), "Label", x, y_top, label_w)
    body_h = p(canvas, body, "Body", body_x, y_top, width - label_w - 0.16 * inch)
    line_y = y_top - body_h - 0.12 * inch
    canvas.setStrokeColor(colors.HexColor("#e5eaf1"))
    canvas.line(x, line_y, x + width, line_y)
    return body_h + 0.27 * inch


def draw_metric(canvas: Canvas, x: float, y: float, width: float, height: float):
    rounded_rect(canvas, x, y, width, height, BLUE, BLUE)
    canvas.setFillColor(colors.white)
    canvas.setFont("Helvetica-Bold", 8.5)
    canvas.drawString(x + 0.22 * inch, y + height - 0.3 * inch, "MEASURED IN THIS REPO")
    canvas.setFont("Helvetica-Bold", 34)
    canvas.drawString(x + 0.22 * inch, y + height - 0.87 * inch, "319M rows/sec")
    p(
        canvas,
        "DuckDB via Node.js processed 1,000,000,000 rows in 3.131 seconds. DuckDB via .NET processed the same file in 3.387 seconds at 295M rows/sec.",
        "Body",
        x + 0.22 * inch,
        y + height - 1.04 * inch,
        width - 0.44 * inch,
    )


def draw_pipeline(canvas: Canvas, x: float, y: float, width: float):
    labels = ["orders.parquet", "DuckDB<br/>parquet scan", "Vectorized<br/>GROUP BY", "Region<br/>revenue"]
    step_w = (width - 0.75 * inch) / 4
    current = x
    for index, label in enumerate(labels):
        rounded_rect(canvas, current, y, step_w, 0.72 * inch, PANEL)
        p(canvas, f"<b>{label}</b>", "Body", current + 0.08 * inch, y + 0.48 * inch, step_w - 0.16 * inch)
        current += step_w
        if index < len(labels) - 1:
            canvas.setFont("Helvetica-Bold", 18)
            canvas.setFillColor(TEAL)
            canvas.drawCentredString(current + 0.125 * inch, y + 0.28 * inch, ">")
            current += 0.25 * inch


def page_one(canvas: Canvas):
    page_background(canvas)
    header(
        canvas,
        "Parquet Performance Benchmark",
        "DuckDB: Embedded Analytics At Parquet Speed",
        "The strongest practical finding is not only raw speed. DuckDB combines SQL ergonomics, direct Parquet scanning, vectorized execution, and in-process deployment without a separate database server.",
    )
    y = 0.95 * inch
    left_x = MARGIN_X
    left_w = 6.0 * inch
    panel_h = 4.0 * inch
    rounded_rect(canvas, left_x, y, left_w, panel_h, PANEL)
    top = y + panel_h - 0.25 * inch
    top -= fact_row(canvas, left_x + 0.25 * inch, top, left_w - 0.5 * inch, "What it is", "An embedded analytical database that runs inside the application process and exposes APIs across .NET, Node.js, Python, R, C/C++, Go, Rust, Java, browser/WASM, and more.")
    top -= fact_row(canvas, left_x + 0.25 * inch, top, left_w - 0.5 * inch, "How it works", "The benchmark queries <b>orders.parquet</b> directly, projects only <b>Quantity</b>, <b>UnitPrice</b>, and <b>RegionId</b>, then performs a SQL <b>GROUP BY</b> in-process.")
    top -= fact_row(canvas, left_x + 0.25 * inch, top, left_w - 0.5 * inch, "Why fast", "DuckDB is built for OLAP scans and uses a columnar-vectorized execution engine, processing batches of values instead of row-by-row interpretation.")
    fact_row(canvas, left_x + 0.25 * inch, top, left_w - 0.5 * inch, "Finding", "In the current 1B-row results, DuckDB delivered the fastest recorded run via Node.js and stayed very close via .NET.")

    right_x = left_x + left_w + 0.38 * inch
    draw_metric(canvas, right_x, y + 1.78 * inch, 5.6 * inch, 2.2 * inch)
    draw_pipeline(canvas, right_x, y + 0.55 * inch, 5.6 * inch)
    footer(canvas, 1, "Sources: repo Results.md; DuckDB official docs on in-process deployment, APIs, OLAP workloads, and vectorized execution.")


def draw_card(canvas: Canvas, x: float, y: float, width: float, height: float, pill: str, title: str, body: str, bullets: list[str]):
    rounded_rect(canvas, x, y, width, height, PANEL)
    canvas.setFillColor(colors.HexColor("#dff7f7"))
    canvas.roundRect(x + 0.22 * inch, y + height - 0.45 * inch, 1.42 * inch, 0.25 * inch, 9, stroke=0, fill=1)
    canvas.setFillColor(TEAL)
    canvas.setFont("Helvetica-Bold", 7.4)
    canvas.drawCentredString(x + 0.93 * inch, y + height - 0.37 * inch, pill.upper())
    title_top = y + height - 0.72 * inch
    title_h = p(canvas, title, "CardTitle", x + 0.22 * inch, title_top, width - 0.44 * inch)
    body_top = title_top - title_h - 0.12 * inch
    body_h = p(canvas, body, "CardBody", x + 0.22 * inch, body_top, width - 0.44 * inch)
    bullet_y = body_top - body_h - 0.22 * inch
    for bullet in bullets:
        canvas.setFillColor(TEAL)
        canvas.circle(x + 0.29 * inch, bullet_y - 0.06 * inch, 2.2, stroke=0, fill=1)
        used = p(canvas, bullet, "CardBody", x + 0.39 * inch, bullet_y, width - 0.6 * inch)
        bullet_y -= used + 0.12 * inch


def page_two(canvas: Canvas):
    page_background(canvas)
    header(
        canvas,
        "Technology Landscape",
        "The Alternative Engines: Different Strengths",
        "The comparison is not only a race. Each technology occupies a different layer of the analytical stack: embedded SQL engine, server OLAP database, columnar foundation, or DataFrame query engine.",
    )
    y = 0.82 * inch
    gap = 0.24 * inch
    card_w = (PAGE_WIDTH - (2 * MARGIN_X) - (2 * gap)) / 3
    card_h = 3.98 * inch
    draw_card(
        canvas,
        MARGIN_X,
        y,
        card_w,
        card_h,
        "Server OLAP",
        "ClickHouse",
        "A high-performance, column-oriented SQL database for OLAP workloads over very large datasets.",
        [
            "Best fit: persistent analytical service, concurrent users, operational database deployment.",
            "Repo setup: Docker container with the repository <b>data</b> folder mapped into ClickHouse.",
            "Benchmark caveat: separate container startup, data loading, and pure query timing.",
        ],
    )
    draw_card(
        canvas,
        MARGIN_X + card_w + gap,
        y,
        card_w,
        card_h,
        "Columnar Foundation",
        "Apache Arrow",
        "A multi-language toolbox and standardized in-memory columnar format for high-performance analytical systems.",
        [
            "Best fit: native systems, interoperability, and low-level columnar processing.",
            "Repo setup: C++ aggregator uses Apache Arrow with Parquet support.",
            "Tradeoff: high control and speed, but more implementation effort than SQL.",
        ],
    )
    draw_card(
        canvas,
        MARGIN_X + (2 * (card_w + gap)),
        y,
        card_w,
        card_h,
        "DataFrame Engine",
        "Polars",
        "A Rust-based DataFrame library available from Python, R, and Node.js, with a vectorized query engine and lazy optimization.",
        [
            "Best fit: data engineering, data science, and expressive DataFrame workflows.",
            "Repo setup: Python aggregator uses Polars with PyArrow for Parquet processing.",
            "Tradeoff: excellent productivity, but API style differs from SQL-oriented tools.",
        ],
    )
    footer(canvas, 2, "Sources: ClickHouse docs; Apache Arrow overview; Polars user guide; repo README and ClickHouse aggregator setup.")


def draw_table(canvas: Canvas, x: float, y_top: float, width: float):
    data = [
        ["Technology", "Best Fit", "Strength", "Tradeoff"],
        ["DuckDB", "Local analytics over files", "SQL, direct Parquet scan, very fast execution", "Less suitable when a shared long-running service is required"],
        ["ClickHouse", "Production OLAP service", "Scalable server-side analytics and operational DB features", "Docker/server lifecycle and load path add complexity"],
        ["Apache Arrow", "Native columnar systems", "Fast low-level columnar processing and interoperability", "More code and engineering effort"],
        ["Polars", "DataFrame analytics", "Productive API, lazy optimization, parallel execution", "Performance depends on query shape and API usage"],
    ]
    table_data = []
    for row_index, row in enumerate(data):
        style = ParagraphStyle(
            name=f"Table{row_index}",
            parent=styles["Body"],
            fontName="Helvetica-Bold" if row_index == 0 or row[0] in {"DuckDB", "ClickHouse", "Apache Arrow", "Polars"} else "Helvetica",
            fontSize=8.2,
            leading=10.5,
            textColor=colors.white if row_index == 0 else TEXT,
        )
        table_data.append([Paragraph(cell, style) for cell in row])
    col_widths = [0.9 * inch, 1.12 * inch, 1.47 * inch, width - 3.49 * inch]
    table = Table(table_data, colWidths=col_widths, repeatRows=1)
    table.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, 0), BLUE),
                ("GRID", (0, 0), (-1, -1), 0.4, LINE),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 7),
                ("RIGHTPADDING", (0, 0), (-1, -1), 7),
                ("TOPPADDING", (0, 0), (-1, -1), 7),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 7),
            ]
        )
    )
    _, table_h = table.wrap(width, PAGE_HEIGHT)
    table.drawOn(canvas, x, y_top - table_h)
    return table_h


def draw_bars(canvas: Canvas, x: float, y: float, width: float, height: float):
    rounded_rect(canvas, x, y, width, height, PANEL)
    p(canvas, "1B-row throughput from Results.md", "CardTitle", x + 0.22 * inch, y + height - 0.25 * inch, width - 0.44 * inch)
    p(canvas, "Rows per second, same generated Parquet input and same region revenue aggregation.", "Body", x + 0.22 * inch, y + height - 0.62 * inch, width - 0.44 * inch)
    rows = [
        ("DuckDB Node", 319),
        ("DuckDB .NET", 295),
        ("Rust Parquet", 252),
        ("C++ Arrow", 242),
        (".NET Parquet", 54),
    ]
    max_value = 319
    current_y = y + height - 1.12 * inch
    for label, value in rows:
        canvas.setFont("Helvetica-Bold", 8.8)
        canvas.setFillColor(colors.HexColor("#354158"))
        canvas.drawString(x + 0.22 * inch, current_y, label)
        track_x = x + 1.55 * inch
        track_w = width - 2.65 * inch
        canvas.setFillColor(colors.HexColor("#e1e8f1"))
        canvas.roundRect(track_x, current_y - 0.03 * inch, track_w, 0.14 * inch, 5, stroke=0, fill=1)
        canvas.setFillColor(colors.HexColor("#2f80ed"))
        canvas.roundRect(track_x, current_y - 0.03 * inch, track_w * (value / max_value), 0.14 * inch, 5, stroke=0, fill=1)
        canvas.drawRightString(x + width - 0.22 * inch, current_y, f"{value}M")
        current_y -= 0.36 * inch
    canvas.setFillColor(WARN)
    canvas.roundRect(x + 0.22 * inch, y + 0.24 * inch, width - 0.44 * inch, 0.55 * inch, 6, stroke=0, fill=1)
    canvas.setFillColor(WARN_LINE)
    canvas.rect(x + 0.22 * inch, y + 0.24 * inch, 0.04 * inch, 0.55 * inch, stroke=0, fill=1)
    p(canvas, "ClickHouse and Polars are described in the comparison, but are not included in this measured chart because matching 1B-row results were not captured in <b>Results.md</b>.", "Small", x + 0.34 * inch, y + 0.67 * inch, width - 0.72 * inch)


def page_three(canvas: Canvas):
    page_background(canvas)
    header(canvas, "Benchmark Interpretation", "What We Learned From The Benchmark", "")
    left_x = MARGIN_X
    top = 6.08 * inch
    table_h = draw_table(canvas, left_x, top, 6.15 * inch)
    takeaway_y = top - table_h - 0.18 * inch
    for text in [
        "DuckDB is the most impressive practical result: near-native scan speed with SQL ergonomics and no database server requirement.",
        "ClickHouse is the stronger choice when the problem becomes a shared analytical service instead of a local file-processing task.",
        "Arrow is the foundation for native columnar performance; Polars is the ergonomic DataFrame path.",
    ]:
        rounded_rect(canvas, left_x, takeaway_y - 0.34 * inch, 6.15 * inch, 0.34 * inch, PALE, PALE)
        p(canvas, text, "Small", left_x + 0.12 * inch, takeaway_y - 0.09 * inch, 5.9 * inch)
        takeaway_y -= 0.43 * inch
    draw_bars(canvas, left_x + 6.48 * inch, 1.08 * inch, 5.56 * inch, 4.88 * inch)
    footer(canvas, 3, "Sources: Results.md; DuckDB, ClickHouse, Apache Arrow, and Polars official documentation. Figures are local benchmark observations, not universal product rankings.")


def main():
    canvas = Canvas(str(OUTPUT), pagesize=(PAGE_WIDTH, PAGE_HEIGHT))
    canvas.setTitle("Parquet Processing Benchmark Findings")
    for renderer in (page_one, page_two, page_three):
        renderer(canvas)
        canvas.showPage()
    canvas.save()
    print(OUTPUT)


if __name__ == "__main__":
    main()
