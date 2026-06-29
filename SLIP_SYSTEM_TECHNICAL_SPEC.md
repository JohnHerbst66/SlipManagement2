# Truck Loading Slip Management System — Technical Specification

**Purpose of this document:** This is the complete technical handoff for continued development of this project. It captures every design decision, schema definition, validation rule, and architectural pattern agreed during planning, plus a precise list of gaps/bugs found in the existing partial implementation. Treat this as the source of truth. Where the existing codebase (`SlipManagement2`) conflicts with this spec, this spec wins.

**Stack:** C# WinForms (.NET), SQLite, Dapper, single-user/single-PC desktop application.

---

## 1. Project Purpose & Context

The system replaces manual paperwork for a mining/quarry/logistics operation. Trucks load material on site; an operator records the load details and prints a physical slip. The slip serves two purposes:

1. A printed record the truck driver carries with the load.
2. A permanent, tamper-resistant digital record proving exactly what left the site and when — the system's core purpose is **anti-theft / proof-of-record**, not just convenience. This single requirement drives most of the stricter rules below (immutable IDs, never-delete semantics, database-level constraints rather than UI-only checks).

**Operating context:**
- Single operator, single PC, no network, no multi-user concurrency to handle.
- High volume: hundreds of slips printed per day.
- Operator already receives a separate daily booking list containing driver names — **Driver Name is deliberately excluded from the slip** to keep data entry fast.
- Printer hardware will be mixed: a dot-matrix printer (Epson LX-350, in hand now, uses multi-part carbon-copy paper) and, in future, a thermal receipt printer (model not yet chosen).
- Customer has already approved a set of WinForms screen mockups (see Section 6) — UI layout is not greenfield, it is largely fixed; this spec governs *behavior and data*, not visual redesign.

---

## 2. Technology & Architecture Decisions

| Decision | Choice | Reasoning |
|---|---|---|
| Database engine | **SQLite** (single file, e.g. `WeighbridgeData.db` next to the executable) | Single user/PC, no server install needed, trivially backed up by copying one file, handles this volume with ease. |
| Data access | **Dapper** (NOT raw ADO.NET, NOT Entity Framework) | Raw ADO.NET is too much boilerplate per query; EF Core's migration/change-tracking machinery is overkill for this scale. Dapper maps query results to POCOs with minimal code while keeping hand-written SQL visible and controllable. |
| Excel export | **ClosedXML** recommended | Free, license-friendly, produces genuine `.xlsx` (current code produces plain CSV — see Section 11, this is a known gap to fix). |
| Printing | `System.Drawing.Printing.PrintDocument` with **explicitly set custom `PaperSize`** in code | Never rely on the Windows driver's default page size — this is the exact root cause of the original "always prints as A4 and wastes paper" complaint. Paper dimensions must always be set from the active `PrinterProfile`, never assumed from the driver. |

### 2.1 Layered architecture (agreed pattern)

```
Models/              Plain C# classes mirroring DB rows. No SQL, no UI code.
    Slip.cs
    FieldConfigEntry.cs
    LookupValue.cs
    PrinterProfile.cs

Data/                 ONLY place SQL/Dapper queries are allowed to live.
    SlipRepository.cs         InsertSlip, UpdateSlip, GetSlipById, GetUnprintedSlips,
                               GetSlipsBetweenDates(status), MarkAsPrinted, VoidSlip(reason)
    LookupRepository.cs       GetValues(table), AddValue(table, value), DeleteValue(table, id)
    FieldConfigRepository.cs  GetAll, SaveAll
    PrinterProfileRepository.cs  GetActive, GetAll, Save

Forms/                WinForms screens. Call repositories only. NEVER contain raw SQL.
    SetupForm.cs (first-run only)
    MainForm.cs
    CreateSlipForm.cs
    PrintPreviewForm.cs
    SlipHistoryForm.cs
    CustomizeSlipsForm.cs
    PrinterSettingsForm.cs
```

**Rule:** if a Form ever contains a `SQLiteCommand` or raw SQL string, that is a violation of the agreed architecture and should be refactored into the matching Repository.

---

## 3. Database Schema (Target — Final Agreed Version)

```sql
CREATE TABLE Slips (
    SlipID       INTEGER PRIMARY KEY AUTOINCREMENT,   -- true DB autoincrement, the rowid itself
    BillNumber   TEXT NOT NULL UNIQUE,                 -- format: yyyyMMdd-HHmmss, generated once at first save
    Status       TEXT NOT NULL DEFAULT 'Unprinted',    -- 'Unprinted' | 'Printed' | 'Voided'
    Field1       TEXT,   -- default label: "Truck Reg"      (required, never hideable)
    Field2       TEXT,   -- default label: "Stockpile Name"
    Field3       TEXT,   -- default label: "Rom Type"
    Field4       TEXT,   -- default label: "Block Nr"
    Field5       TEXT,   -- default label: "Size"
    Field6       TEXT,   -- default label: "Destination"
    Field7       TEXT,   -- default label: "Tons"           (required before print, never hideable)
    Field8       TEXT,   -- default label: "Order Number"
    Field9       TEXT,   -- default label: "Slot"
    Field10      TEXT,   -- default label: "Client"
    VoidReason   TEXT,   -- max 20 chars, set only when Status = 'Voided'
    CreatedAt    TEXT NOT NULL DEFAULT (datetime('now')),
    PrintedAt    TEXT     -- set only when Status transitions to 'Printed'
);

-- Database-level protection: BillNumber must never change after being set, even by a future
-- application bug. This is intentional defense-in-depth — do not remove this trigger.
CREATE TRIGGER PreventBillNumberChange
BEFORE UPDATE OF BillNumber ON Slips
WHEN OLD.BillNumber <> NEW.BillNumber
BEGIN
    SELECT RAISE(ABORT, 'BillNumber cannot be changed once set.');
END;

-- SlipID requires NO trigger: as the INTEGER PRIMARY KEY AUTOINCREMENT column (SQLite rowid alias),
-- it is structurally immutable once inserted, provided application code never issues
-- "UPDATE Slips SET SlipID = ..." (it must not, and no UI should ever expose SlipID as editable).

CREATE TABLE FieldConfig (
    FieldSlot    TEXT PRIMARY KEY,      -- 'Field1' .. 'Field10'
    LabelName    TEXT NOT NULL,          -- max 14 characters, enforced in application code/UI
    OrderLine    INTEGER NOT NULL,
    Hidden       INTEGER NOT NULL DEFAULT 0   -- 0 = visible, 1 = hidden.
                                                -- Rows for Field1 and Field7 must NEVER be settable to 1.
                                                -- Enforce by disabling/greying that checkbox/cell in the
                                                -- Customize Slips UI for those two specific rows.
);

-- Lookup tables. Deliberately NOT foreign-keyed to Slips. They exist ONLY to populate dropdown
-- suggestions for fast data entry. The Slip itself always stores the plain text value at the
-- moment of creation, never a foreign key reference. This means renaming or deleting a lookup
-- value later NEVER alters a historical slip's printed/exported text — critical for the
-- proof-of-record requirement. Applies to 7 of the 10 fields (see Section 4 for which ones).
CREATE TABLE TruckRegs     (ID INTEGER PRIMARY KEY AUTOINCREMENT, Value TEXT NOT NULL UNIQUE);
CREATE TABLE StockpileRefs (ID INTEGER PRIMARY KEY AUTOINCREMENT, Value TEXT NOT NULL UNIQUE);
CREATE TABLE ROMTypes      (ID INTEGER PRIMARY KEY AUTOINCREMENT, Value TEXT NOT NULL UNIQUE);
CREATE TABLE BlockNrs      (ID INTEGER PRIMARY KEY AUTOINCREMENT, Value TEXT NOT NULL UNIQUE);
CREATE TABLE Sizes         (ID INTEGER PRIMARY KEY AUTOINCREMENT, Value TEXT NOT NULL UNIQUE);
CREATE TABLE Clients       (ID INTEGER PRIMARY KEY AUTOINCREMENT, Value TEXT NOT NULL UNIQUE);
CREATE TABLE Destinations  (ID INTEGER PRIMARY KEY AUTOINCREMENT, Value TEXT NOT NULL UNIQUE);
-- Order Number (Field8) and Slot (Field9) are manual free-text entry only — no lookup table,
-- no dropdown, per original requirements gathering.

CREATE TABLE PrinterProfiles (
    ProfileID      INTEGER PRIMARY KEY AUTOINCREMENT,
    ProfileName    TEXT    NOT NULL DEFAULT '' UNIQUE,  -- human name, e.g. "Default", "Dot Matrix - Small"
    PrinterName    TEXT    NOT NULL,           -- Windows printer name, e.g. "EPSON LX-350"
    Mode           TEXT    NOT NULL,           -- paper size profile key: 'Small240x102', 'A4', etc.
    WidthMM        DECIMAL NOT NULL,
    HeightMM       DECIMAL,                    -- NULL/ignored for ContinuousRoll
    MarginTopMM    DECIMAL NOT NULL DEFAULT 0,
    MarginLeftMM   DECIMAL NOT NULL DEFAULT 0,
    MarginRightMM  DECIMAL NOT NULL DEFAULT 0,
    MarginBottomMM DECIMAL NOT NULL DEFAULT 0,
    NumCopies      INTEGER NOT NULL DEFAULT 1,
    Orientation    TEXT    NOT NULL DEFAULT 'Portrait',   -- 'Portrait' | 'Landscape'
    SlipLengthIn   REAL    NOT NULL DEFAULT 5.5,          -- custom slip height in inches (Small240x102 only)
    IsActive       INTEGER NOT NULL DEFAULT 0             -- exactly one row has IsActive=1 at all times
);
-- A "Default" preset row (Small240x102, 10mm margins, 1 copy, Portrait, 5.5 in) is seeded
-- automatically on first run so the app always has a usable active profile, even if the
-- operator skips Printer Settings during First-Time Setup.

CREATE TABLE GlobalSettings (
    SettingKey   TEXT PRIMARY KEY,    -- e.g. 'HeaderTitle', 'LogoPath'
    SettingValue TEXT DEFAULT ''
);
```

### 3.1 Key schema design notes

- **Why `Field1`...`Field10` instead of named columns:** the customer explicitly wants to relabel, reorder, and hide fields themselves via the Customize Slips screen, without a developer. Generic columns + `FieldConfig` driving the label/order/visibility is the agreed trade-off. Cost of this trade-off: `Tons` (Field7) is stored as `TEXT`, not a native `DECIMAL`, so the "must be a valid number" rule has to be enforced entirely in application code (see Section 4.2), not by the database column type.
- **Why lookup tables aren't foreign keys:** explained above — preserves historical accuracy of printed/exported records even if lookup lists are edited or pruned later.
- **Why `PrinterProfiles` isn't linked to a Slip:** reprinting an old slip always uses whichever profile is *currently* active, not whatever was active when it was first printed. The slip does not need to remember which printer/profile produced its first printout.
- **`Status` is a text enum, not a boolean flag.** The existing partial implementation uses an `IsPrinted` integer flag — this is insufficient because it cannot represent the third state, `Voided`. Migrate to `Status TEXT` with three values.

---

## 4. Field Definitions (Final Agreed List)

### 4.1 Locked / automatic fields (never user-editable, ever)

| Field | Type | Generation | Format/Example | Mutability |
|---|---|---|---|---|
| SlipID | `INTEGER` (DB autoincrement) | System, at first save | `41`, `42`, `43`... | Never changes, never reused even if the slip is later voided. |
| BillNumber | `TEXT` | System, at first save | `20260625-082559` (i.e. `yyyyMMdd-HHmmss`, C#: `DateTime.Now.ToString("yyyyMMdd-HHmmss")`) | Never changes, protected by DB trigger. Never reused even if voided. Uniqueness relies on the `UNIQUE` constraint as a cheap backstop; no sequence-collision handling needed — single-user manual entry makes same-second collisions practically negligible, and a rejected insert is an acceptable (if unlikely) outcome. |

### 4.2 Editable fields (Field1–Field10)

| # | DB column | Default label | Required? | Can be hidden? | Input style |
|---|---|---|---|---|---|
| 1 | Field1 | Truck Reg | **Yes — required to save even as a draft** | **No — never hideable** | Dropdown (ComboBox, `DropDownStyle = DropDown`, not `DropDownList`) + manual typing of new values |
| 2 | Field2 | Stockpile Name | Optional | Yes | Dropdown + manual |
| 3 | Field3 | Rom Type | Optional | Yes | Dropdown + manual |
| 4 | Field4 | Block Nr | Optional | Yes | Dropdown + manual |
| 5 | Field5 | Size | Optional | Yes | Dropdown + manual |
| 6 | Field6 | Destination | Optional | Yes (flagged as likely to be renamed to "Slot Number" eventually — plain relabel via Customize Slips, no schema change needed) | Dropdown + manual |
| 7 | Field7 | Tons | **Yes — required before the slip can be printed; may be blank while still a draft (Unprinted)** | **No — never hideable** | Manual numeric entry only (no dropdown) |
| 8 | Field8 | Order Number | Optional | Yes | Manual text only (no lookup table) |
| 9 | Field9 | Slot | Optional | Yes | Manual text only (no lookup table) |
| 10 | Field10 | Client | Optional | Yes | Dropdown + manual |

**Driver Name was deliberately excluded** — not a field, not in the schema. The operator gets this from a separate daily booking list; adding it here would only slow down data entry for no benefit.

### 4.3 Dropdown-with-manual-entry behavior (applies to Fields 1, 2, 3, 4, 5, 6, 10)

- When the operator types a value not already in that field's lookup list and saves, **auto-save the new value to the relevant lookup table** so it's available next time.
- Provide a simple management UI (can be one shared screen with a "which list?" selector) where the operator can **delete** entries from a lookup list — e.g. to remove a typo.
- Deleting a lookup entry **never** affects any slip that already used that text value, because the slip stores plain text, not a foreign key.

### 4.4 Validation rules (precise)

- **Field labels** (in Customize Slips): hard max **14 characters**. Enforced via `MaxLength` on the label textbox/grid cell, not just a post-save check — should be physically impossible to type past 14.
- **Tons (Field7):**
  - Accepts digits and **either a comma or a period** as the decimal separator (common local input habit). Internally convert any comma to a period before storing/validating (`value.Replace(',', '.')`).
  - Reject anything else: letters, multiple separators (e.g. `12.5.3`), etc.
  - May be **blank** while the slip's Status is `Unprinted`.
  - The **Print button must be disabled** whenever Tons is blank or fails validation.
  - If Print is somehow triggered anyway (e.g. stale UI state), show a clear inline message and do not print — never produce a slip with invalid/missing tonnage.
- **Truck Reg (Field1):** required before the Save button will even create a draft row. Not allowed to be blank at any lifecycle stage.
- **No duplicate-label or blank-label validation** in Customize Slips. This was a deliberate decision — it's the company's own configuration screen, and the customer explicitly said they're fine taking responsibility for their own label choices here. Do not add this validation.
- **Void reason:** free text, hard max **20 characters**, enforced via `MaxLength` directly on the textbox control.

---

## 5. Slip Lifecycle (State Machine)

Three states, stored in `Slips.Status`:

```
        Save                          Print (after preview confirm)
UNPRINTED ────────────► (tile on Main Page) ────────────► PRINTED ──► (reprint anytime, stays Printed)
   │
   │ Cancel + reason (max 20 chars)
   ▼
VOIDED  (kept permanently, never deleted, visible in a separate Voided view)
```

### 5.1 Unprinted (draft)

- Created the moment the operator clicks **Save** on a new slip — written to the DB immediately, not held only in memory. (This is a deviation from the partial existing implementation, which currently keeps unsaved tiles purely in a UI `Dictionary` — see Section 11.)
- Appears as a tile on the Main Page (shows Truck Reg, SlipID, Tons).
- Fully editable, including re-opening from its tile.
- Can be **voided** (see 5.3) or can proceed to **Print**.
- Re-opening an existing unprinted tile and saving again **reuses the same SlipID/BillNumber** — does not allocate new ones.

### 5.2 Printed (final record)

- Reached only via the Print Preview → Confirm Print step (never directly).
- On confirmation: `Status` → `'Printed'`, `PrintedAt` set, tile removed from Main Page, slip now appears in Slip History permanently.
- **SlipID and BillNumber become permanently locked** (already enforced from creation, but this is the point at which "locked forever" actually starts mattering practically).
- All *other* fields remain editable afterward (e.g. fixing a typo in Destination) via the Slip History → Edit tab.
- Can be **reprinted** any number of times from History, with no special restriction.

### 5.3 Voided (cancelled draft)

- Only reachable from `Unprinted` — a `Printed` slip cannot be voided, only edited.
- Requires the operator to type a reason (≤20 characters) — this is mandatory, not optional.
- The row is **never deleted** — `Status` → `'Voided'`, `VoidReason` populated, row stays in the table permanently.
- Visible in a separate "Voided" view/tab, distinct from the main Printed-only History list.
- The SlipID and BillNumber consumed by a voided slip are **never reused**. A resulting gap in the SlipID sequence (e.g. 49, 52, 53 — no 50, 51) is intentional and acceptable: for an audit trail, a gap with a recorded void reason is a *feature*, not a defect — it proves nothing was silently erased.

### 5.4 Why nothing is ever hard-deleted

Every row that ever receives a SlipID must remain queryable forever, in one of exactly two states: Printed or Voided (with a reason). This guarantees that "what happened to slip #50?" always has an answer in the system itself.

---

## 6. Screens (7 total) & UI Logic

### 6.0 First-Time Setup (appears exactly once)

- Triggered when the app starts and the SQLite database file does not yet exist on disk.
- Collects: company header text, logo image path (optional file picker), default printer (dropdown of installed printers), paper size (same options as Printer Settings), and slip length in inches (shown only when `Small240x102` is selected).
- **Slip length validation:** if the paper size is `Small240x102`, the slip length field must contain a positive number before "Get Started" is allowed to proceed; a clear inline message is shown on failure.
- **Save as preset checkbox** (checked by default): if ticked, the entered printer/paper/slip-length values are saved as a named printer preset (name defaults to `"Default"`). If unticked, the seeded `"Default"` preset from `SeedPrinterProfileIfEmpty` remains in place as the active fallback.
- **"Customize Fields..." button:** opens `CustomizeSlipsForm` as a non-blocking dialog from within setup. The default field labels remain untouched unless the operator actively changes something. This button is an optional shortcut, not a required step.
- "Get Started" button: creates the database (all tables + the `BillNumber` trigger), inserts default `FieldConfig` rows, saves company/logo/printer settings, creates the named preset (if checked), then closes the form so `Program.cs` can open the Main Page.
- Never shown again — every subsequent launch checks "does the DB file exist" and goes straight to Main Page.
- Everything chosen here remains editable later via Customize Slips / Printer Settings.

### 6.1 Main Page

- Default landing screen (after first run).
- Shows a tile per `Unprinted` slip: Truck Reg, SlipID, Tons.
- **Empty state:** if there are zero unprinted tiles, show a message instead of blank space — agreed wording: *"Welcome. Your slips will appear here. Click New Slip to get started."*
- **Many tiles:** the tile panel must be scrollable; there is no fixed cap on concurrent in-progress trucks.
- Buttons: New Slip, Slip History, Printer Settings, Customize Slips, Exit.

### 6.2 Create / Edit Slip

- Used both for a brand-new slip and for re-opening an existing unprinted tile.
- SlipID and BillNumber displayed but **never editable** (read-only controls, not merely disabled — should remain visibly present at all times).
- All other fields per Section 4 (dropdown-with-manual-entry vs plain text, per field).
- Buttons: **Save**, **Cancel**, **Print**.
  - Save → writes/updates the row as `Unprinted` (or `Status` stays `Unprinted` if editing an existing draft), tile appears/updates on Main Page.
  - Cancel → if nothing has been saved yet, simply closes with no DB write at all (genuinely discarded, not a void — void only applies to an *already-saved* draft, see 5.3).
  - Print → must **always** ensure the slip is persisted to the DB first (either it was already saved, or save it implicitly at this point), then open **Print Preview**. Print must never be reachable as a path that produces a printed slip with no corresponding database row.

### 6.3 Print Preview

- Renders the slip **exactly** as it will physically print, using the *same* rendering routine that the real print job uses — no separate "preview-only" drawing code, to guarantee zero mismatch between preview and output.
- Offers a way to jump to Printer Settings to adjust margins/paper/etc., then **returns to this same preview** afterward (loop back, not forward) so the operator re-checks layout before committing.
- **Confirm Print** button: sends the job to the printer, sets `Status` → `'Printed'` and `PrintedAt`, removes the tile from Main Page, returns the operator to the Main Page.

### 6.4 Slip History

- Date range filter (From/To).
- Grid of `Printed` slips (default view) with dynamic per-field filter dropdowns driven by `FieldConfig` labels.
- Selecting a row shows full slip details with a separate **Edit** tab — every field editable **except SlipID and BillNumber**.
- **Print** button on a selected row = reprint (always allowed).
- **Export** button → produces a real `.xlsx` (see Section 7).
- Separate **Voided** view/tab: same kind of grid, but filtered to `Status = 'Voided'`, with the `VoidReason` column shown.

### 6.5 Customize Slips

- Tab 1 — **Field Setup**: grid of FieldSlot (read-only) / Label Name (editable, 14-char max) / Order Line / Hidden checkbox.
  - The Hidden checkbox/cell for the rows mapped to **Field1 (Truck Reg)** and **Field7 (Tons)** must be disabled/greyed — these two can never be hidden, full stop.
- Tab 2 — **Slip Design**: company header text, logo file path + Browse button, and a checklist confirming exactly which fields print on the final slip (this should reflect/derive from the same Hidden flags as Tab 1, not a second independent source of truth).
- No validation against duplicate/blank labels (deliberate, see Section 4.4).

### 6.6 Printer Settings

- **Preset row** (top of the form): a `ComboBox` listing all named presets plus a `"— New preset... —"` option.
  - Selecting an **existing preset** loads its values into the form fields and immediately sets it as the active preset in the database (GlobalSettings are synced so `SlipPrintEngine` picks up the change).
  - Selecting `"— New preset... —"` reveals a name text-box and populates sensible defaults. The name field determines whether Save creates a new row or updates an existing one — no separate "Save As" control is needed.
- **Form fields** (both columns): printer (from `PrinterSettings.InstalledPrinters`), paper size profile, orientation, number of copies, slip length (inches), print margins (mm ×4), company/header name.
- **Save button**: validates slip length, resolves paper dimensions, calls `SaveOrUpdatePrinterProfile` (upsert by `ProfileName`), then re-selects the saved preset in the dropdown. Company name (`HeaderTitle`) is saved to GlobalSettings since it is global, not per-preset.
- Settings are stored in `PrinterProfiles` (one row per named preset, `IsActive=1` marks the current selection). GlobalSettings are kept in sync after every save or preset selection so `SlipPrintEngine` can continue reading from GlobalSettings without modification.
- A **Calibration / Test Print** button (planned, not yet implemented) will print a ruled grid so margins can be measured on-site.

---

## 7. Printing System — Detailed Design

- **Two printer behavior modes**, stored per-profile as `Mode`:
  - `FixedSize` — dot-matrix / cut-form paper with a known fixed width and height.
  - `ContinuousRoll` — thermal receipt paper: fixed width, but height/length calculated dynamically from how many visible fields there are (no wasted blank roll, no cut-off content).
- **Never trust the printer driver's default page size.** Always construct an explicit `PaperSize` object from the active `PrinterProfile`'s `WidthMM`/`HeightMM` and assign it directly to `PrintDocument.DefaultPageSettings.PaperSize`. This is the fix for the original "always prints A4" complaint.
- **NumCopies** is a real, independent setting even though the current dot-matrix paper is multi-part/carbon-copy (which produces physical duplicates per single print pass regardless of software). Keep the setting anyway — paper stock may change, or a genuine second printout may be wanted for an unrelated reason.
- **Print Preview and the final print job must share the same rendering function.** Do not maintain two separate drawing code paths.
- **Calibration/Test Print:** prints a measurement grid at the profile's configured dimensions, used once per new printer/site to dial in margins precisely — this is a deliberate "fine-tune once at install time" tool, not a day-to-day operator feature.
- **Thermal printer guidance (not yet purchased):** prefer a model that exposes a genuine Windows GDI/driver-based printing mode over one that only supports raw ESC/POS commands — this lets it reuse the exact same `PrintDocument`-based code path as the dot-matrix printer. If only ESC/POS is available, that becomes a second, separate printing implementation — defer building that until the hardware is actually chosen, since building it speculatively risks wasted/wrong work.
- **Printer-unavailable handling:** wrap the print call in a try/catch; on failure, show a clear, plain-language message (e.g. "Could not connect to the printer — check it's switched on and connected, then try again"), keep the slip's data intact, and allow retry without losing anything.

---

## 8. Reporting / Excel Export

- **Two separate export actions**, not one combined export with a filter toggle:
  1. **Export Printed Slips** — date range, `Status = 'Printed'` only.
  2. **Export Voided Slips** — separate report, `Status = 'Voided'` only, includes the `VoidReason` column.
- Column headers in the exported file use the **current `FieldConfig` labels**, so the spreadsheet always matches whatever the operator currently sees on screen — not hardcoded `Field1`/`Field2` names.
- Always include `SlipID`, `BillNumber`, and `Status` as fixed leading columns regardless of any relabeling.
- Include **all ten Field columns even if currently marked Hidden** — "hidden" only means "doesn't print on the physical slip," not "excluded from the data record."
- Implement as **one shared export routine** parameterized by status filter (and whether to include the `VoidReason` column), not two duplicated code paths.
- Must produce a genuine `.xlsx` file (recommend ClosedXML) — not a CSV renamed to look like Excel (see Section 11 for why the current CSV approach is a real risk, not just a format nicety).

---

## 9. Backup Strategy

- **Automatic:** on app startup (or once per day, whichever comes first), silently copy the SQLite DB file to a local `Backups/` subfolder with a timestamped filename (e.g. `slips_backup_20260625.db`). Keep a rolling window (~30 days) and delete older backups automatically.
- **Manual:** a "Backup Now" action lets the operator pick a destination via a save dialog (e.g. a USB drive) for an off-PC copy, with a confirmation showing where it was saved.
- Recommend actually performing one real restore-from-backup test during setup, to confirm the process genuinely works before relying on it.

---

## 10. Security Posture

- **No login/PIN.** Physical access to the PC is the entire security boundary — consistent with single-user, single-PC deployment. If multi-user or multi-site use is ever introduced later, this would need revisiting, but is explicitly out of scope for now.

---

## 11. Current Implementation Status — Known Gaps Found In Existing Code

**Decision: start with a fresh database.** The existing `WeighbridgeData.db` file contains only test data and will be **discarded, not migrated**. Delete the existing `.db` file (and any `ALTER TABLE`/incremental-patch logic built around it) and let the corrected schema in Section 3 be created from scratch via the First-Time Setup flow (Section 6.0). Do not write a migration script for the old data — it is not needed and would be wasted effort for data that isn't being kept.

The existing partial implementation (`SlipManagement2`) already has real, useful work in it — particularly the adaptive paper-size print engine and the dynamic field-customization concept — and that **code** should be built on, not discarded. It's specifically the **data** (the old `.db` file and its incomplete/incorrect schema) that gets thrown away, not the application logic. A code review against this spec found the following gaps that need fixing. These are listed in priority order.

### 11.1 Blocking bugs (the app currently errors when run)

- The `slips` table is created **without** `IsPrinted` or `CreatedAt` columns, but `LoadSavedSlipsToDashboard`, `MarkSlipAsPrinted`, and the entire `SlipsHistoryForm` query against both columns. This throws a SQL error every time the Main Page loads, every time a slip is marked printed, and every time History is opened. **Fix: delete the existing `.db` file, and rebuild `DatabaseManager.InitializeDatabase()` to create the `Status TEXT` + `CreatedAt TEXT` schema in Section 3 from scratch, not a boolean `IsPrinted` flag** (boolean can't represent `Voided`, and there is no old data to preserve).

### 11.2 Violations of the core proof-of-record requirement

- **Print does not require Save first.** `CreateSlip.btnPrint_Click` calls `SlipPrintEngine.ExecutePrintJob` directly without ever calling `SaveSlip`. An operator going New Slip → fill in → Print (skipping Save) currently produces a printed slip with **no corresponding database row at all**. Fix per Section 6.2: Print must always guarantee persistence first.
- **SlipID is editable.** `SlipsHistoryForm`'s Edit tab includes `SlipID` in its editable textboxes and writes it back via `UPDATE slips SET SlipID = ...`. This must be removed — SlipID must never appear as an editable field anywhere (BillNumber is correctly already excluded from that same edit list — apply the same treatment to SlipID).
- **No database-level trigger protecting BillNumber.** Currently "protected" only by one form choosing not to expose it in its UI — add the `PreventBillNumberChange` trigger from Section 3 so the protection holds regardless of which code path attempts the change.
- **Print Preview is built but never invoked.** `PrintSlipPreview.cs` exists as a form but nothing in the app ever opens it — both `CreateSlip` and `SlipsHistoryForm` call `SlipPrintEngine.ExecutePrintJob` directly, sending straight to the printer with zero preview. Fix per Section 6.3: wire Print → open Print Preview → Confirm → *then* call the print engine.

### 11.3 Missing features (expected — plan only just finalized these)

- No Void/Voided workflow exists at all yet: no `VoidReason` column, no void action, no Voided view.
- No `PrinterProfiles` table — current settings are a single flat set of global key-value pairs (one printer/size at a time), not multiple named, switchable profiles.
- No lookup tables (`TruckRegs`, `Destinations`, etc.) exist yet — current fields are plain `TextBox` controls, not dropdown-with-manual-entry `ComboBox` controls, and there's no auto-save-new-value or delete-from-list behavior.
- No First-Time Setup screen yet.
- No backup functionality yet.
- No validation anywhere yet: Tons isn't checked as a decimal or comma-converted, Truck Reg isn't enforced as required, no 14-character label cap exists, no required-before-print gating on the Print button.

### 11.4 Smaller defects to correct

- `CustomizeSlipsForm`'s grid and checklist currently allow `Hidden = 1` to be set for Field1 (Truck Reg) and Field7 (Tons) — these two specific rows' Hidden control must be disabled.
- `CreateSlip`'s constructor only loops `i = 1` to `i <= 7` when applying custom labels/visibility from `FieldConfig` to the on-screen controls — Fields 8/9/10 (Order Number, Slot, Client) never receive their customized label or hidden state on the data-entry form, even though they're correctly saved, loaded, filtered, and printed elsewhere. The loop bound must become `i <= 10`.
- Export is currently raw CSV via manual string-joining (`File.WriteAllLines`), with **no quoting/escaping** — a single comma inside any field's value will silently corrupt the row structure. Replace with a real `.xlsx` writer (ClosedXML) per Section 8, and split into the two separate Printed/Voided exports.
- Default paper profile fallback is hardcoded to `"A4"` in both `PrinterSettingsForm` and `SlipPrintEngine` — this is the literal original complaint ("always prints A4, wastes paper") still present as the silent default. Should not silently default to A4; should force explicit configuration during First-Time Setup instead.
- `SlipID` is currently a free-text `TEXT` column on the `slips` table (with `BilNumber` as the actual `PRIMARY KEY`), and the "next ID" is computed by scanning `MAX(CAST(SlipID AS INTEGER))` on every load. This is backwards relative to the agreed schema and is fragile (relies on SlipID always parsing as an integer, which a free-text field can't guarantee). Fix: make `SlipID` the real `INTEGER PRIMARY KEY AUTOINCREMENT`, and `BillNumber` a separate `UNIQUE TEXT` column, per Section 3.

---

## 12. Agreed UI Logic Flow (Operator Journey — Canonical Reference)

This is the exact decision logic agreed for the Create/Print flow, to guide implementation of event handlers and state transitions:

```
Main Page
  └─ [New Slip] or [click an existing unprinted tile]
        └─ Create/Edit Slip screen opens (blank, or pre-filled from the tile)
              └─ Operator fills in fields, then chooses:
                    ├─ [Save]   → Status stays/becomes 'Unprinted', row persisted, tile shown/updated on Main Page, form closes
                    ├─ [Cancel] → if never saved: discard with no DB write
                    │             if editing an already-saved draft and choosing to void: prompt for
                    │             reason (≤20 chars) → Status = 'Voided', VoidReason set, row kept, tile removed
                    └─ [Print]  → ensure persisted (Save implicitly if not already) → open Print Preview
                                       └─ Print Preview shows exact render
                                             ├─ [Adjust Printer Settings] → opens Printer Settings →
                                             │     on return, loops BACK to the same Print Preview (not forward)
                                             └─ [Confirm Print] → send to printer → Status = 'Printed',
                                                   PrintedAt set → tile removed from Main Page →
                                                   return to Main Page
```

All three terminal outcomes (Saved-as-Unprinted, Voided, Confirmed-Printed) ultimately return the operator to the Main Page, which is always the application's home screen.

---

## 13. Open Items (Not Yet Decided — Flagged, Not Resolved)

- Whether a confirmation step ("are you sure?") should precede voiding, separate from the mandatory reason prompt — left as a judgment call for whenever that screen is actually built.
- Exact thermal printer model — not yet chosen; when it is, confirm GDI/Windows-driver support before purchase (Section 7).
- A genuine backup-restore test has not yet been performed — recommended once initial setup is stable.

---

*End of specification. This document reflects all decisions made during the planning phase as of 25 June 2026, plus a full code-level review of the existing partial implementation against that plan.*
