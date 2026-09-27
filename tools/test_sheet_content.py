import copy
from pathlib import Path
import unittest
from sheet_content import validate, validate_runtime_texts, local_tables, read_xlsx, ROOT


class ContentTests(unittest.TestCase):
    def setUp(self):
        self.rows = local_tables(ROOT/'Data/Sheets')

    def test_samples_and_multiline_text_survive(self):
        result = validate(self.rows)
        self.assertEqual(len(result['Characters']), 5)
        self.assertEqual(len(result['Perks']), 12)
        self.assertTrue(any('\n' in r['ko'] for r in result['Texts']))

    def test_duplicate_id_is_rejected(self):
        self.rows['Characters'].append(self.rows['Characters'][1][:])
        with self.assertRaisesRegex(ValueError, 'duplicate ID'):
            validate(self.rows)

    def test_missing_foreign_key_is_rejected(self):
        self.rows['CharacterPerks'][1][2] = 'missing'
        with self.assertRaisesRegex(ValueError, 'missing reference'):
            validate(self.rows)

    def test_missing_tid_is_rejected(self):
        self.rows['Characters'][1][1] = 'missing'
        with self.assertRaisesRegex(ValueError, 'missing TID'):
            validate(self.rows)

    def test_blank_zero_nan_and_out_of_range_are_distinct(self):
        for value in ['', 'nan', 'inf', '-1', '1.01']:
            rows = copy.deepcopy(self.rows)
            rows['Events'][1][3] = value
            with self.subTest(value=value), self.assertRaises(ValueError):
                validate(rows)
        self.rows['Events'][1][3] = '0'
        self.assertEqual(next(r for r in validate(self.rows)['Events'] if r['id'] == 'friend_invitation')['base_probability'], 0)

    def test_placeholder_mismatch_is_rejected(self):
        self.rows['Texts'][1][1] = '{name}님'
        self.rows['Texts'][1][2] = '{person}'
        with self.assertRaisesRegex(ValueError, 'placeholders'):
            validate(self.rows)

    def test_duplicate_group_position_is_rejected(self):
        self.rows['OfficeTextGroups'][2][2] = self.rows['OfficeTextGroups'][1][2]
        with self.assertRaisesRegex(ValueError, 'expected positions'):
            validate(self.rows)

    def test_blank_numeric_value_does_not_become_zero(self):
        self.rows['Characters'][1][2] = ''
        with self.assertRaisesRegex(ValueError, 'required number'):
            validate(self.rows)

    def test_unknown_effect_is_rejected(self):
        self.rows['PerkEffects'][1][2] = 'execute_code'
        with self.assertRaisesRegex(ValueError, 'unsupported target'):
            validate(self.rows)

    def test_missing_ui_key_blocks_build(self):
        bundle = validate(self.rows)
        bundle['Texts'] = [r for r in bundle['Texts'] if r['tid'] != 'settings.language.en']
        with self.assertRaisesRegex(ValueError, 'Runtime text IDs missing'):
            validate_runtime_texts(bundle)


if __name__ == '__main__':
    unittest.main()
