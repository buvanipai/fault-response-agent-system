"""
BDG2 Data Preparation Script

Processes real building energy data from the Building Data Genome Project 2
(buds-lab/building-data-genome-project-2) and converts it to JSON for the
fault response agent system.

Data source: https://github.com/buds-lab/building-data-genome-project-2
Published in: Nature Scientific Data (2020)

Prerequisites:
  1. Clone BDG2 repo with LFS into data/raw/:
     git clone --depth 1 --filter=blob:none --sparse \\
       https://github.com/buds-lab/building-data-genome-project-2.git data/raw
     cd data/raw
     git sparse-checkout set data/meters/cleaned data/metadata data/weather
     git lfs pull
  2. Run: ./venv/bin/python scripts/prepare_data.py
"""

import json
import sys
from pathlib import Path

import pandas as pd

# ──────────────────────────────────────────────────────────────────────────────
# Configuration
# ──────────────────────────────────────────────────────────────────────────────

SCRIPT_DIR = Path(__file__).parent
PROJECT_ROOT = SCRIPT_DIR.parent
BDG2_DIR = PROJECT_ROOT / "data" / "raw" / "data"
OUTPUT_DIR = (
    PROJECT_ROOT / "src" / "FaultResponseSystem" / "Data" / "SampleData"
)

# Paths to actual BDG2 files
METER_FILES = {
    "Electricity": BDG2_DIR / "meters" / "cleaned" / "electricity_cleaned.csv",
    "ChilledWater": BDG2_DIR / "meters" / "cleaned" / "chilledwater_cleaned.csv",
    "Steam": BDG2_DIR / "meters" / "cleaned" / "steam_cleaned.csv",
    "HotWater": BDG2_DIR / "meters" / "cleaned" / "hotwater_cleaned.csv",
}
METADATA_FILE = BDG2_DIR / "metadata" / "metadata.csv"
WEATHER_FILE = BDG2_DIR / "weather" / "weather.csv"

# Target site and time range
TARGET_SITE = "Panther"
START_DATE = "2017-07-01"
END_DATE = "2017-07-08"  # 1 full week
MAX_BUILDINGS_PER_TYPE = 10


def safe_float(val, default=0.0) -> float:
    try:
        v = float(val)
        return round(v, 2) if not pd.isna(v) else default
    except (ValueError, TypeError):
        return default


def safe_int(val, default=0) -> int:
    try:
        v = float(val)
        return int(v) if not pd.isna(v) else default
    except (ValueError, TypeError):
        return default


def process_metadata() -> pd.DataFrame:
    """Load and filter BDG2 metadata.csv to target site."""
    print("\n── Processing metadata ──")
    df = pd.read_csv(METADATA_FILE)
    print(f"  Total buildings in BDG2: {len(df)}")
    print(f"  Columns: {list(df.columns[:10])}...")

    # Filter to target site
    site_df = df[df["site_id"] == TARGET_SITE].copy()
    print(f"  {TARGET_SITE} buildings: {len(site_df)}")
    print(f"  Uses: {site_df['primaryspaceusage'].value_counts().to_dict()}")

    return site_df


def process_meters(
    meter_type: str, csv_path: Path, site_building_ids: list[str]
) -> list[dict]:
    """
    Convert wide-format BDG2 meter CSV to long-format JSON records.

    BDG2 format: timestamp, Building1_name, Building2_name, ...
    Each column is a building, values are hourly readings in kWh/kBtu.
    """
    print(f"\n── Processing {meter_type} meters ──")

    if not csv_path.exists():
        print(f"  ⚠ File not found: {csv_path}")
        return []

    df = pd.read_csv(csv_path, parse_dates=["timestamp"])
    print(f"  Total columns: {len(df.columns) - 1} meters")

    # Find columns for our site's buildings
    # BDG2 column names look like: Panther_office_Hannah, Panther_lodging_Cora
    site_cols = [c for c in df.columns if c.startswith(f"{TARGET_SITE}_")]
    # Also filter to only buildings that are in our metadata
    site_cols = [c for c in site_cols if c in site_building_ids]

    if not site_cols:
        # Try matching without exact metadata match (some buildings may only
        # appear in certain meter files)
        site_cols = [c for c in df.columns if c.startswith(f"{TARGET_SITE}_")]

    site_cols = site_cols[:MAX_BUILDINGS_PER_TYPE]
    print(f"  {TARGET_SITE} buildings with {meter_type}: {len(site_cols)}")

    # Filter time range
    df = df[(df["timestamp"] >= START_DATE) & (df["timestamp"] < END_DATE)]
    print(f"  Time range: {df['timestamp'].min()} → {df['timestamp'].max()}")
    print(f"  Hours: {len(df)}")

    # Convert wide → long format
    records = []
    for col in site_cols:
        meter_df = df[["timestamp", col]].dropna(subset=[col])
        for _, row in meter_df.iterrows():
            records.append({
                "MeterId": f"{col}_{meter_type.lower()}",
                "BuildingId": col,
                "Timestamp": row["timestamp"].isoformat(),
                "MeterType": meter_type,
                "Value": round(float(row[col]), 2),
            })

    print(f"  Generated {len(records)} readings")
    return records


def process_weather() -> list[dict]:
    """Extract BDG2 weather data for target site and time range."""
    print("\n── Processing weather ──")
    df = pd.read_csv(WEATHER_FILE, parse_dates=["timestamp"])
    print(f"  Total weather records: {len(df)}")

    # Filter to site and time range
    # BDG2 weather columns: timestamp, site_id, airTemperature, cloudCoverage,
    #   dewTemperature, precipDepth1HR, precipDepth6HR, seaLvlPressure,
    #   windDirection, windSpeed
    df = df[df["site_id"] == TARGET_SITE]
    df = df[(df["timestamp"] >= START_DATE) & (df["timestamp"] < END_DATE)]
    print(f"  {TARGET_SITE} weather records in range: {len(df)}")

    records = []
    for _, row in df.iterrows():
        records.append({
            "SiteId": TARGET_SITE,
            "Timestamp": row["timestamp"].isoformat(),
            "AirTemperature": safe_float(row.get("airTemperature")),
            "DewTemperature": safe_float(row.get("dewTemperature")),
            "WindSpeed": safe_float(row.get("windSpeed")),
            "WindDirection": safe_int(row.get("windDirection")),
            "CloudCoverage": safe_int(row.get("cloudCoverage")),
            "PrecipDepth": safe_float(row.get("precipDepth1HR", 0)),
            "SeaLevelPressure": safe_float(row.get("seaLvlPressure")),
        })

    return records


def build_metadata_json(
    meta_df: pd.DataFrame, meter_records: list[dict]
) -> list[dict]:
    """Build buildings.json from BDG2 metadata + discovered meter IDs."""
    print("\n── Building metadata JSON ──")

    # Group meter IDs by building
    meter_map: dict[str, set[str]] = {}
    for rec in meter_records:
        bid = rec["BuildingId"]
        mid = rec["MeterId"]
        if bid not in meter_map:
            meter_map[bid] = set()
        meter_map[bid].add(mid)

    buildings = []
    for _, row in meta_df.iterrows():
        bid = row["building_id"]
        if bid not in meter_map:
            continue  # skip buildings with no meter data in our subset

        buildings.append({
            "BuildingId": bid,
            "SiteId": row["site_id"],
            "PrimaryUse": str(row.get("primaryspaceusage", "Unknown")),
            "SquareFeet": safe_int(row.get("sqft", 0)),
            "YearBuilt": safe_int(row.get("yearbuilt", 0)),
            "FloorCount": safe_int(row.get("numberoffloors", 0)),
            "Timezone": str(row.get("timezone", "US/Eastern")),
            "MeterIds": sorted(list(meter_map[bid])),
        })

    print(f"  {len(buildings)} buildings with meter data in our subset")
    return buildings


def save_json(data, filename: str):
    """Save data as JSON to the output directory."""
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    path = OUTPUT_DIR / filename
    with open(path, "w") as f:
        json.dump(data, f, indent=2)
    size_kb = path.stat().st_size / 1024
    count = len(data) if isinstance(data, list) else "N/A"
    print(f"  → Saved {filename} ({size_kb:.1f} KB, {count} records)")


def main():
    print("=" * 60)
    print("BDG2 Data Preparation for Fault Response Agent System")
    print(f"Site: {TARGET_SITE} | Range: {START_DATE} → {END_DATE}")
    print(f"Source: {BDG2_DIR}")
    print("=" * 60)

    # Verify BDG2 data exists
    if not METADATA_FILE.exists():
        print(f"\n✗ BDG2 data not found at {BDG2_DIR}")
        print("  Please clone the BDG2 repo first. See script docstring.")
        sys.exit(1)

    # Step 1: Process metadata
    meta_df = process_metadata()
    site_building_ids = meta_df["building_id"].tolist()

    # Step 2: Process each meter type
    all_meter_records = []
    for meter_type, csv_path in METER_FILES.items():
        records = process_meters(meter_type, csv_path, site_building_ids)
        all_meter_records.extend(records)

    print(f"\n  Total meter readings: {len(all_meter_records)}")

    # Step 3: Process weather
    weather_records = process_weather()

    # Step 4: Build buildings metadata JSON
    buildings = build_metadata_json(meta_df, all_meter_records)

    # Step 5: Save all JSON files
    print("\n💾 Saving JSON files...")
    save_json(all_meter_records, "meters.json")
    save_json(weather_records, "weather.json")
    save_json(buildings, "buildings.json")
    # NOTE: maintenance.json and compliance_rules.json are manually curated
    #       (no BDG2 equivalent) — don't overwrite them

    # Summary
    unique_buildings = set(r["BuildingId"] for r in all_meter_records)
    unique_meters = set(r["MeterId"] for r in all_meter_records)
    meter_types = sorted(set(r["MeterType"] for r in all_meter_records))

    print("\n" + "=" * 60)
    print("✅ Data preparation complete!")
    print(f"   Buildings:      {len(unique_buildings)}")
    print(f"   Meters:         {len(unique_meters)}")
    print(f"   Meter readings: {len(all_meter_records)}")
    print(f"   Weather records:{len(weather_records)}")
    print(f"   Meter types:    {meter_types}")
    print(f"   Output:         {OUTPUT_DIR}")
    print("=" * 60)


if __name__ == "__main__":
    main()
