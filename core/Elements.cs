using System;
using System.Collections.Generic;
using System.Linq;

namespace Cosmos.Core;

[Flags]
public enum ElementRole { None = 0, Gas = 1, Ice = 2, Rock = 4, Metal = 8, Carbon = 16, Radio = 32 }
public sealed record Element(string Id, string Name, double Density, uint Colour, ElementRole Roles);

public static class ElementCatalog
{
    public static readonly IReadOnlyList<Element> Elements = Array.AsReadOnly(new[]
    {
        new Element("gas", "Khí nhẹ", .3, 0xB8C7FF, ElementRole.Gas),
        new Element("ice", "Băng", .9, 0x4DE5FF, ElementRole.Ice),
        new Element("rock", "Đá", 3, 0xE58040, ElementRole.Rock),
        new Element("metal", "Kim loại", 8, 0xFFD966, ElementRole.Metal),
        new Element("carbon", "Carbon", 2, 0xBF66F2, ElementRole.Carbon),
        new Element("radio", "Phóng xạ", 10, 0x66FF4D, ElementRole.Radio)
    });
    public static string[] Names => Elements.Select(e => e.Name).ToArray();
    public static uint[] Colours => Elements.Select(e => e.Colour).ToArray();
    public static int Elem(ElementRole role) => Enumerable.Range(0, Elements.Count).FirstOrDefault(e => (Elements[e].Roles & role) != 0, -1);
}

public sealed partial class World
{
    public readonly IReadOnlyList<Element> Elements;
    public readonly int ElementCount;
    readonly Dictionary<string, int> _elementIds = new(StringComparer.Ordinal);
    readonly Dictionary<ElementRole, int[]> _roleElements = new();
    readonly bool _legacyElements;

    public int Elem(string id) => _elementIds.TryGetValue(id, out int index) ? index : -1;
    public int Elem(ElementRole role) => _roleElements[role].Length == 0 ? -1 : _roleElements[role][0];

    void IndexElements()
    {
        for (int e = 0; e < ElementCount; e++)
        {
            Element item = Elements[e];
            if (item == null || string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Name)
                || !double.IsFinite(item.Density) || item.Density <= 0 || !_elementIds.TryAdd(item.Id, e))
                throw new ArgumentException("Elements require unique non-empty ids/names and positive finite densities.");
        }
        foreach (ElementRole role in Enum.GetValues<ElementRole>())
            _roleElements.Add(role, Enumerable.Range(0, ElementCount).Where(e => role != ElementRole.None && (Elements[e].Roles & role) != 0).ToArray());
    }

    public double[] Mix(params (string Id, double Share)[] entries)
    {
        var mix = new double[ElementCount];
        foreach (var (id, share) in entries)
        {
            int e = Elem(id);
            if (e < 0) throw new ArgumentException($"Unknown element '{id}'.");
            mix[e] += share;
        }
        return mix;
    }

    public double Matter(int i, ElementRole role)
    {
        double mass = 0; int offset = i * ElementCount; int[] indices = _roleElements[role];
        if (indices.Length == 1) return Comp[offset + indices[0]];
        foreach (int e in indices) mass += Comp[offset + e];
        return mass;
    }

    public double Share(int i, ElementRole role) => Matter(i, role) / M[i];

    // Proportional loss across every group carrying this role. The single-group path keeps legacy arithmetic exact.
    void LoseMatter(int i, ElementRole role, double loss)
    {
        int[] indices = _roleElements[role]; int offset = i * ElementCount;
        if (indices.Length == 1) { Comp[offset + indices[0]] -= loss; return; }
        double available = Matter(i, role);
        if (available <= 0) return;
        foreach (int e in indices) Comp[offset + e] -= loss * (Comp[offset + e] / available);
    }

    void ScaleMatter(int i, ElementRole role, double factor)
    {
        int offset = i * ElementCount;
        foreach (int e in _roleElements[role]) Comp[offset + e] *= factor;
    }

    void HashElements(Action<ulong> mix)
    {
        // The original catalogue is implicit in old hashes. Explicit custom tables carry all their metadata.
        if (_legacyElements) return;
        mix((ulong)ElementCount);
        foreach (Element e in Elements)
        {
            mix((ulong)e.Id.Length); foreach (char c in e.Id) mix(c);
            mix((ulong)e.Name.Length); foreach (char c in e.Name) mix(c);
            mix(BitConverter.DoubleToUInt64Bits(e.Density)); mix(e.Colour); mix((ulong)e.Roles);
        }
    }
}
