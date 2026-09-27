"""Build-time Google Sheets importer. Python 3 standard library only.

Spec Ref: Assets/Specification/DataAuthoring/GoogleSheetsContent.md
"""
import argparse
import csv
import hashlib
import io
import json
import math
import os
from pathlib import Path
import re
import sys
import tempfile
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
SCHEMA = {
    'Texts': 'tid ko en',
    'Characters': 'id name_tid affinity focus response inquiry',
    'Perks': 'id name_tid description_tid',
    'CharacterPerks': 'id character_id perk_id',
    'PerkEffects': 'id perk_id target tags_all operation value',
    'Events': 'id name_tid tags base_probability cooldown_days min_friends min_closeness task_id required_flags',
    'Tasks': 'id name_tid stat tags difficulty duration_hours',
    'Relationships': 'id character_id other_id kind closeness',
    'OfficeTextGroups': 'id group position text_tid',
}
GROUPS = dict(Names=3, Roles=3, Titles=7, Senders=7, Bodies=7, Options=14, Reasons=14, Results=14)
NS = {'s': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}


def read_xlsx(data):
    """Read plain cells, preserving strings/newlines and rejecting formula/error cells."""
    result = {}
    with zipfile.ZipFile(io.BytesIO(data)) as z:
        if sum(f.file_size for f in z.infolist()) > 32 * 1024 * 1024:
            raise ValueError('Workbook exceeds 32 MB uncompressed limit')
        strings = []
        if 'xl/sharedStrings.xml' in z.namelist():
            strings = [''.join(t.text or '' for t in e.iterfind('.//s:t', NS))
                       for e in ET.fromstring(z.read('xl/sharedStrings.xml'))]
        rels = {r.attrib['Id']: r.attrib['Target'] for r in
                ET.fromstring(z.read('xl/_rels/workbook.xml.rels'))}
        for sheet in ET.fromstring(z.read('xl/workbook.xml')).findall('s:sheets/s:sheet', NS):
            name = sheet.attrib['name']
            if name not in SCHEMA:
                continue
            rid = sheet.attrib['{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id']
            target = rels[rid]
            target = target.lstrip('/') if target.startswith('/') else 'xl/' + target
            rows = []
            for row in ET.fromstring(z.read(target)).findall('s:sheetData/s:row', NS):
                cells = {}
                for cell in row.findall('s:c', NS):
                    if cell.find('s:f', NS) is not None or cell.attrib.get('t') == 'e':
                        raise ValueError(f'{name}!{cell.attrib["r"]}: formulas/errors are not supported')
                    col = re.match('[A-Z]+', cell.attrib['r'])[0]
                    index = 0
                    for c in col:
                        index = index * 26 + ord(c) - 64
                    value = cell.findtext('s:v', '', NS)
                    if cell.attrib.get('t') == 's':
                        value = strings[int(value)]
                    elif cell.attrib.get('t') == 'inlineStr':
                        value = ''.join(t.text or '' for t in cell.iterfind('.//s:t', NS))
                    cells[index - 1] = value
                rows.append([cells.get(i, '') for i in range(max(cells, default=-1) + 1)])
            result[name] = rows
    return result


def validate(raw):
    tables = {}
    for name, fields in SCHEMA.items():
        rows = raw.get(name)
        if not rows:
            raise ValueError(f'{name}: missing/empty sheet')
        headers = fields.split()
        actual = list(rows[0])
        while actual and actual[-1] == '':
            actual.pop()
        if actual != headers:
            raise ValueError(f'{name}: expected columns {headers}, got {actual}')
        parsed, seen = [], set()
        for line, row in enumerate(rows[1:], 2):
            if not any(str(v) for v in row):
                continue
            if len(row) > len(headers) and any(row[len(headers):]):
                raise ValueError(f'{name}:{line}: unexpected extra columns')
            values = [str(v) for v in row[:len(headers)]]
            values += [''] * (len(headers) - len(values))
            item = dict(zip(headers, values))
            key = values[0]
            if not re.fullmatch(r'[A-Za-z0-9_.-]+', key) or key in seen:
                raise ValueError(f'{name}:{line}: invalid/duplicate ID {key!r}')
            seen.add(key)
            parsed.append(item)
        if not parsed:
            raise ValueError(f'{name}: no data rows')
        tables[name] = parsed
    ids = {n: {r[next(iter(r))] for r in rows} for n, rows in tables.items()}

    def number(row, key, low, high, integer=False):
        try:
            n = float(row[key])
        except ValueError:
            raise ValueError(f'{row.get("id")}.{key}: required number') from None
        if not math.isfinite(n) or not low <= n <= high or (integer and n != int(n)):
            raise ValueError(f'{row.get("id")}.{key}: out of range {low}..{high}')
        row[key] = int(n) if integer else n

    refs = {'character_id': 'Characters', 'other_id': 'Characters', 'perk_id': 'Perks', 'task_id': 'Tasks'}
    for name, rows in tables.items():
        for row in rows:
            for key, value in row.items():
                if key.endswith('_tid') and value not in ids['Texts']:
                    raise ValueError(f'{name}.{row["id"]}.{key}: missing TID {value!r}')
                if key in refs and not (key == 'task_id' and value == '') and value not in ids[refs[key]]:
                    raise ValueError(f'{name}.{row["id"]}.{key}: missing reference {value!r}')
                if key in ('tags', 'tags_all', 'required_flags') and value:
                    tags = value.split(';')
                    if len(set(tags)) != len(tags) or any(not re.fullmatch('[a-z0-9_]+', t) for t in tags):
                        raise ValueError(f'{row["id"]}.{key}: invalid/duplicate tags')
    for row in tables['Texts']:
        expected = set(re.findall(r'\{[A-Za-z_][A-Za-z_0-9]*\}', row['ko']))
        if row['en'] and set(re.findall(r'\{[A-Za-z_][A-Za-z_0-9]*\}', row['en'])) != expected:
            raise ValueError(f'{row["tid"]}: translation placeholders differ')
    for row in tables['Characters']:
        for key in ('affinity', 'focus', 'response', 'inquiry'):
            number(row, key, 0, 100, True)
    for row in tables['Tasks']:
        if row['stat'] not in ('affinity', 'focus', 'response', 'inquiry'):
            raise ValueError(f'{row["id"]}: unsupported stat')
        number(row, 'difficulty', 0, 100, True)
        number(row, 'duration_hours', 0.01, 1000)
    for row in tables['Events']:
        number(row, 'base_probability', 0, 1)
        for key, high in [('cooldown_days', 3650), ('min_friends', 1000), ('min_closeness', 100)]:
            number(row, key, 0, high, True)
    targets = {'event_probability':'multiply','task_success':'add','task_duration':'multiply',
               'failure_stress':'add','acceptance_stress':'add'}
    for row in tables['PerkEffects']:
        if targets.get(row['target']) != row['operation']:
            raise ValueError(f'{row["id"]}: unsupported target/operation')
        number(row, 'value', 0 if row['operation'] == 'multiply' else -100, 100)
    for name, fields in [('CharacterPerks', ('character_id','perk_id')), ('Relationships', ('character_id','other_id'))]:
        pairs = [tuple(r[f] for f in fields) for r in tables[name]]
        if len(set(pairs)) != len(pairs):
            raise ValueError(f'{name}: duplicate relationship')
    for row in tables['Relationships']:
        number(row, 'closeness', 0, 100, True)
        if row['kind'] != 'friend' or row['character_id'] == row['other_id']:
            raise ValueError(f'{row["id"]}: invalid relationship')
    for row in tables['OfficeTextGroups']:
        if row['group'] not in GROUPS:
            raise ValueError(f'{row["id"]}: unknown office group')
        number(row, 'position', 0, GROUPS[row['group']] - 1, True)
    for name, count in GROUPS.items():
        positions = sorted(r['position'] for r in tables['OfficeTextGroups'] if r['group'] == name)
        if positions != list(range(count)):
            raise ValueError(f'OfficeTextGroups.{name}: expected positions 0..{count-1} exactly once')
    return {'schema_version': 1, **{n: sorted(rows, key=lambda r: str(next(iter(r.values())))) for n, rows in tables.items()}}


def local_tables(directory):
    result = {}
    for name in SCHEMA:
        with (directory / (name + '.csv')).open(encoding='utf-8-sig', newline='') as source:
            result[name] = list(csv.reader(source))
    return result


def validate_runtime_texts(bundle):
    known = {row['tid'] for row in bundle['Texts']}
    required = {'settings.language.' + mode for mode in ('ko', 'en', 'tid')}
    for name in ('OfficeDesktop.cs', 'OfficeScenario.cs'):
        source = (ROOT/'Assets/MilestonePrototype/Runtime'/name).read_text(encoding='utf-8')
        required.update(re.findall(r'SheetContent\.(?:T|Format)\("([A-Za-z0-9_.-]+)"\s*[,)]', source))
    missing = sorted(required - known)
    if missing:
        raise ValueError('Runtime text IDs missing: ' + ', '.join(missing))


def download(book_id):
    if not re.fullmatch('[A-Za-z0-9_-]+', book_id):
        raise ValueError('Invalid spreadsheet ID')
    url = f'https://docs.google.com/spreadsheets/d/{book_id}/export?format=xlsx'
    with urllib.request.urlopen(url, timeout=45) as response:
        data = response.read(16 * 1024 * 1024 + 1)
    if len(data) > 16 * 1024 * 1024 or not data.startswith(b'PK'):
        raise ValueError('Expected public XLSX download. Enable Anyone with the link / Viewer.')
    return data


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--local', type=Path, help='Explicit CSV input for offline reproduction/tests')
    p.add_argument('--output', type=Path, default=ROOT/'Assets/MilestonePrototype/Resources/sheet-content.json')
    args = p.parse_args()
    if args.local:
        raw = local_tables(args.local)
        source = {'mode':'local_csv'}
    else:
        config = json.loads((ROOT/'tools/sheet-sources.json').read_text(encoding='utf-8'))
        raw, source = {}, {'mode':'google_sheets'}
        for key in ('text', 'game_data'):
            payload = download(config[key])
            extracted = read_xlsx(payload)
            if set(raw) & set(extracted):
                raise ValueError('Duplicate tables across workbooks')
            raw.update(extracted)
            source[key] = {'id':config[key], 'sha256':hashlib.sha256(payload).hexdigest()}
    bundle = validate(raw)
    validate_runtime_texts(bundle)
    bundle['source'] = source
    # Validate the complete pair before replacing the one runtime file.
    encoded = json.dumps(bundle, ensure_ascii=False, sort_keys=True, indent=2) + '\n'
    args.output.parent.mkdir(parents=True, exist_ok=True)
    fd, temp = tempfile.mkstemp(dir=args.output.parent, suffix='.tmp')
    try:
        with os.fdopen(fd, 'w', encoding='utf-8', newline='\n') as f:
            f.write(encoded)
        os.replace(temp, args.output)
    finally:
        if os.path.exists(temp):
            os.unlink(temp)
    if not args.local:
        snapshot = ROOT/'Data/Sheets'
        snapshot.mkdir(parents=True, exist_ok=True)
        for name in SCHEMA:
            with (snapshot/(name+'.csv')).open('w', encoding='utf-8', newline='') as f:
                csv.writer(f).writerows(raw[name])
    missing = sum(not r['en'] for r in bundle['Texts'])
    print(f'SHEET_CONTENT_OK texts={len(bundle["Texts"])} characters={len(bundle["Characters"])} missing_en={missing} sha256={hashlib.sha256(encoded.encode()).hexdigest()}')


if __name__ == '__main__':
    try:
        main()
    except Exception as ex:
        print(f'SHEET_CONTENT_FAILED: {ex}', file=sys.stderr)
        sys.exit(1)
