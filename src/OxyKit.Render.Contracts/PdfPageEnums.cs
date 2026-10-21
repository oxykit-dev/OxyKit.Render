namespace OxyKit.Render.Contracts;

/// <summary>PDF page sizes exposed by the API (mapped to PeachPDF at emit time).</summary>
public enum PdfPageSize
{
    A0,
    A1,
    A2,
    A3,
    A4,
    A5,
    A6,
    RA0,
    RA1,
    RA2,
    RA3,
    RA4,
    RA5,
    B0,
    B1,
    B2,
    B3,
    B4,
    B5,
    Quarto,
    Foolscap,
    Executive,
    GovernmentLetter,
    Letter,
    Legal,
    Ledger,
    Tabloid,
    Post,
    Crown,
    LargePost,
    Demy,
    Medium,
    Royal,
    Elephant,
    DoubleDemy,
    QuadDemy,
    STMT,
    Folio,
    Statement,
    Size10x14,
}

/// <summary>PDF page orientation exposed by the API.</summary>
public enum PdfPageOrientation
{
    Portrait,
    Landscape,
}
