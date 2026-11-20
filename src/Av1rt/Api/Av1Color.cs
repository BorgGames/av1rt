#nullable enable

namespace Av1rt;

/// <summary>Colour primaries signalled in the sequence header (ITU-T H.273 code points, as in libaom).</summary>
public enum Av1ColorPrimaries
{
    /// <summary>BT.709, also sRGB's primaries.</summary>
    Bt709 = 1,
    /// <summary>Not signalled: the decoder chooses.</summary>
    Unspecified = 2,
    /// <summary>BT.470 System M (historical).</summary>
    Bt470M = 4,
    /// <summary>BT.470 System B, G (historical); BT.601 625-line.</summary>
    Bt470BG = 5,
    /// <summary>BT.601 525-line (SMPTE 170M).</summary>
    Bt601 = 6,
    /// <summary>SMPTE 240M.</summary>
    Smpte240 = 7,
    /// <summary>Generic film (colour filters using illuminant C).</summary>
    GenericFilm = 8,
    /// <summary>BT.2020, BT.2100.</summary>
    Bt2020 = 9,
    /// <summary>SMPTE ST 428 (CIE 1931 XYZ).</summary>
    Xyz = 10,
    /// <summary>SMPTE RP 431-2 (DCI-P3).</summary>
    Smpte431 = 11,
    /// <summary>SMPTE EG 432-1 (Display P3).</summary>
    Smpte432 = 12,
    /// <summary>EBU Tech. 3213-E.</summary>
    Ebu3213 = 22,
}

/// <summary>Transfer characteristics signalled in the sequence header (ITU-T H.273 code points, as in libaom).</summary>
public enum Av1TransferCharacteristics
{
    /// <summary>BT.709.</summary>
    Bt709 = 1,
    /// <summary>Not signalled: the decoder chooses.</summary>
    Unspecified = 2,
    /// <summary>BT.470 System M (historical).</summary>
    Bt470M = 4,
    /// <summary>BT.470 System B, G (historical).</summary>
    Bt470BG = 5,
    /// <summary>BT.601.</summary>
    Bt601 = 6,
    /// <summary>SMPTE 240M.</summary>
    Smpte240 = 7,
    /// <summary>Linear.</summary>
    Linear = 8,
    /// <summary>Logarithmic, 100:1 range.</summary>
    Log100 = 9,
    /// <summary>Logarithmic, 100 * sqrt(10):1 range.</summary>
    Log100Sqrt10 = 10,
    /// <summary>IEC 61966-2-4.</summary>
    Iec61966 = 11,
    /// <summary>BT.1361.</summary>
    Bt1361 = 12,
    /// <summary>sRGB or sYCC.</summary>
    Srgb = 13,
    /// <summary>BT.2020, 10-bit systems.</summary>
    Bt2020TenBit = 14,
    /// <summary>BT.2020, 12-bit systems.</summary>
    Bt2020TwelveBit = 15,
    /// <summary>SMPTE ST 2084, BT.2100 PQ.</summary>
    Smpte2084 = 16,
    /// <summary>SMPTE ST 428.</summary>
    Smpte428 = 17,
    /// <summary>BT.2100 HLG, ARIB STD-B67.</summary>
    Hlg = 18,
}

/// <summary>
/// Matrix coefficients signalled in the sequence header (ITU-T H.273 code points, as in libaom):
/// how the decoder turns YCbCr back into RGB. Identity is left out: AV1 allows it only for 4:4:4.
/// </summary>
public enum Av1MatrixCoefficients
{
    /// <summary>BT.709.</summary>
    Bt709 = 1,
    /// <summary>Not signalled: the decoder chooses.</summary>
    Unspecified = 2,
    /// <summary>US FCC 73.628.</summary>
    Fcc = 4,
    /// <summary>BT.470 System B, G (historical); BT.601 625-line.</summary>
    Bt470BG = 5,
    /// <summary>BT.601 525-line (SMPTE 170M).</summary>
    Bt601 = 6,
    /// <summary>SMPTE 240M.</summary>
    Smpte240 = 7,
    /// <summary>YCgCo.</summary>
    YCgCo = 8,
    /// <summary>BT.2020 non-constant luminance, BT.2100 YCbCr.</summary>
    Bt2020Ncl = 9,
    /// <summary>BT.2020 constant luminance.</summary>
    Bt2020Cl = 10,
    /// <summary>SMPTE ST 2085 YDzDx.</summary>
    Smpte2085 = 11,
    /// <summary>Chromaticity-derived non-constant luminance.</summary>
    ChromatNcl = 12,
    /// <summary>Chromaticity-derived constant luminance.</summary>
    ChromatCl = 13,
    /// <summary>BT.2100 ICtCp.</summary>
    ICtCp = 14,
}
