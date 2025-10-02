namespace D3Edit.Core
{
    public static class CountSafe
    {
        public static int Of(object? o) =>
            o switch
            {
                D3Edit.Filetypes.Gam.CharactersJsonFile c when c?.Records != null => c.Records.Count,
                D3Edit.Filetypes.Gam.CurrencyJsonFile cu when cu?.Records != null => cu.Records.Count,
                D3Edit.Filetypes.Gam.GameFileCollection g when g?.Items != null => g.Items.Count,
                D3Edit.Filetypes.Gam.AffixJsonFile a when a?.Records != null => a.Records.Count,
                D3Edit.Filetypes.Gam.ExperienceTableJsonFile et when et?.Records != null => et.Records.Count,
                D3Edit.Filetypes.Gam.ExperienceAltTableJsonFile eta when eta?.Records != null => eta.Records.Count,
                D3Edit.Filetypes.Gam.HandicapLevelsJsonFile hl when hl?.Records != null => hl.Records.Count,
                D3Edit.Filetypes.Gam.HirelingsJsonFile h when h?.Records != null => h.Records.Count,
                D3Edit.Filetypes.Gam.ItemSalvageLevelsJsonFile s when s?.Records != null => s.Records.Count,
                D3Edit.Filetypes.Gam.ItemTypesJsonFile t when t?.Records != null => t.Records.Count,
                D3Edit.Filetypes.Gam.LabelGBIDsJsonFile lbl when lbl?.Records != null => lbl.Records.Count,
                D3Edit.Filetypes.Gam.MonsterAffixesJsonFile m when m?.Records != null => m.Records.Count,
                D3Edit.Filetypes.Gam.MonsterLevelsJsonFile ml when ml?.Records != null => ml.Records.Count,
                D3Edit.Filetypes.Gam.MonsterNamesJsonFile n when n?.Records != null => n.Records.Count,
                D3Edit.Filetypes.Gam.ParagonBonusesJsonFile p when p?.Records != null => p.Records.Count,
                D3Edit.Filetypes.Gam.PowerFormulaTablesJsonFile pf when pf?.Records != null => pf.Records.Count,
                D3Edit.Filetypes.Gam.RareItemNamesJsonFile r when r?.Records != null => r.Records.Count,
                D3Edit.Filetypes.Gam.RecipesJsonFile rec when rec?.Records != null => rec.Records.Count,
                D3Edit.Filetypes.Gam.SetItemBonusesJsonFile sib when sib?.Records != null => sib.Records.Count,
                D3Edit.Filetypes.Gam.SocketedEffectsJsonFile se when se?.Records != null => se.Records.Count,
                D3Edit.Filetypes.Gam.TransmuteRecipesJsonFile tr when tr?.Records != null => tr.Records.Count,
                D3Edit.Filetypes.Gam.EnchantCostScalarsJsonFile f when f?.Records != null => f.Records.Count,
                D3Edit.Filetypes.Gam.LegacyItemConversionsJsonFile lc when lc?.Records != null => lc.Records.Count,
                D3Edit.Filetypes.Gam.TieredLootRunLevelsJsonFile tl when tl?.Records != null => tl.Records.Count,
                _ => 0
            };
    }
}
