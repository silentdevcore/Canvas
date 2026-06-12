import React, { useState, useEffect, useRef, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { FiCode, FiCopy, FiDownload, FiPlay, FiRefreshCw, FiGitMerge, FiLayout } from 'react-icons/fi';
import Editor, { DiffEditor, type OnMount } from '@monaco-editor/react';
import AppHeader from '@/components/Layout/AppHeader';
import { useEditorStore } from '@/store';

// Framework ids for the report → Canvas Designer flows (output is a design, not C# code).
const REPORT_ID = 'DevExpressReport';
const RDL_REPORT_ID = 'RdlReport';
const RPX_REPORT_ID = 'RpxReport';
const FRX_REPORT_ID = 'FrxReport';
// All report flows post to the same /report-to-design endpoint (the backend auto-detects the format).
const isReportDesign = (id: string) =>
  id === REPORT_ID || id === RDL_REPORT_ID || id === RPX_REPORT_ID || id === FRX_REPORT_ID;

interface Framework {
  id: string;
  name: string;
  status: string;
  description: string;
}

interface Diagnostic {
  code: string;
  severity: string;
  message: string;
}

const API_BASE = '/api/migration';

const FRAMEWORKS_FALLBACK: Framework[] = [
  { id: 'Syncfusion', name: 'Syncfusion PDF',    status: 'full',    description: 'Full pattern-based conversion with top-left coordinate adapter. Covers document/page/text/line/rectangle/image/save.' },
  { id: 'iText7',     name: 'iText7',            status: 'full',    description: 'Roslyn-based conversion: PdfWriter+PdfDocument+Document → PdfDocument; Paragraph (with SetFontSize) → DrawTextFromTop; ShowTextAligned → DrawText; PdfCanvas line/rect/text; Close/SetMargins removed.' },
  { id: 'Apryse',     name: 'Apryse (PDFTron)',  status: 'full',    description: 'Roslyn-based conversion: PDFDoc → PdfDocument, PageCreate+PagePushBack → AddPage(), doc.Save() → document.Save().' },
  { id: 'Aspose',     name: 'Aspose.PDF',        status: 'full',    description: 'Roslyn-based conversion: Document → PdfDocument, Pages.Add → AddPage, TextFragment/TextBuilder with Position → DrawText/DrawTextFromTop.' },
  { id: 'DsPdf',      name: 'DsPdf (GrapeCity)', status: 'full',    description: 'Roslyn-based conversion: GcPdfDocument → PdfDocument; doc.NewPage() → AddPage(); page.Graphics.DrawString/DrawLine/DrawRectangle/FillRectangle → DrawTextFromTop/DrawLineFromTop/DrawRectangleFromTop; doc.Save() preserved.' },
  { id: 'Foxit',      name: 'Foxit PDF SDK',     status: 'full',    description: 'Roslyn-based conversion: PDFDoc → PdfDocument; InsertPage/CreatePage → AddPage; Library.Initialize + GetGraphics/GenerateContent removed; graphics.DrawText/DrawLine/DrawRect/FillRect → DrawTextFromTop/DrawLineFromTop/DrawRectangleFromTop; doc.Save/SaveAs → document.Save().' },
  { id: 'DevExpress', name: 'DevExpress PDF',    status: 'full',    description: 'Roslyn-based conversion: PdfDocumentProcessor → PdfDocument, RenderNewPage → AddPage, draw calls repositioned, SaveDocument → Save. Forms/signatures/report export produce warnings.' },
  { id: 'IronPdf',    name: 'IronPDF',           status: 'pilot',   description: 'Roslyn-based pilot: ChromePdfRenderer → PdfDocument + AddPage scaffold; SaveAs → document.Save(); HTML/URL/Razor rendering calls replaced with diagnostics for manual Canvas draw call migration.' },
  { id: 'Spire',      name: 'Spire.PDF',         status: 'full',    description: 'Roslyn-based full conversion: PdfDocument + Pages.Add → AddPage; Canvas.DrawString → DrawTextFromTop; Canvas.DrawLine → DrawLineFromTop; Canvas.DrawRectangle/FillRectangle → DrawRectangleFromTop; SaveToFile → Save; tables/forms/annotations produce warnings.' },
  { id: 'GemBox',     name: 'GemBox.Pdf',        status: 'full',    description: 'Roslyn-based full conversion: PdfDocument + Pages.Add → AddPage; Content.DrawText → DrawTextFromTop; Content.DrawLine → DrawLineFromTop; Content.DrawRectangle → DrawRectangleFromTop; ComponentInfo.SetLicense removed; forms/encryption/annotations produce warnings.' },
  { id: 'PdfKitNet',  name: 'PDFKit.NET',        status: 'full',    description: 'Roslyn-based full conversion: Document + NewPage/Pages.Add → AddPage; DrawText/DrawString → DrawTextFromTop; DrawLine → DrawLineFromTop; DrawRectangle → DrawRectangleFromTop; Save/Render → Save; forms/encryption/annotations produce warnings. Package identity must be manually verified.' },
  { id: 'Leadtools',  name: 'LEADTOOLS',         status: 'full',    description: 'Roslyn-based full conversion: PDFDocument + AddPage/Pages.Add → AddPage; DrawText/DrawString → DrawTextFromTop; DrawLine → DrawLineFromTop; DrawRectangle → DrawRectangleFromTop; Save/Export → Save; raster/OCR/barcode/conversion APIs produce warnings.' },
  { id: 'ActivePdf',  name: 'ActivePDF',         status: 'pilot',   description: 'Cautious Roslyn pilot for likely Toolkit-style generation; DocConverter, WebGrabber, COM/server, printer, merge, and stamp workflows are manual.' },
  { id: 'PdfTools',   name: 'PDFTools / Pdftools SDK', status: 'pilot', description: 'Cautious Roslyn pilot: removes Sdk.Initialize and flags SDK conversion/processing workflows for manual Canvas.Pdf migration. Direct PDF generation belongs to the separate PDF Toolbox SDK/add-on.' },
  { id: 'PdfToolsToolbox', name: 'PDF Toolbox SDK', status: 'pilot', description: 'Cautious Roslyn pilot for Toolbox direct-generation flows: Document.Create/Page.Create/TextGenerator.ShowLine → Canvas.Pdf; existing-PDF editing and rich styling remain manual.' },
];

const SYNCFUSION_EXAMPLE = `using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;

using var document = new PdfDocument();
var page = document.Pages.Add();
page.Graphics.DrawString(
    "Hello from Syncfusion",
    new PdfStandardFont(PdfFontFamily.Helvetica, 14),
    PdfBrushes.Black,
    40, 40);
document.Save("output.pdf");`;

const ITEXT7_EXAMPLE = `using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;

using var writer = new PdfWriter(outputPath);
using var pdf = new PdfDocument(writer);
using var document = new Document(pdf, PageSize.A4);
document.SetMargins(72, 72, 72, 72);

document.ShowTextAligned(new Paragraph("Invoice #2024").SetFontSize(18), 72, 760, TextAlignment.LEFT);
document.ShowTextAligned(new Paragraph("Thank you for your order."), 72, 720, TextAlignment.LEFT);
document.ShowTextAligned(new Paragraph("Total: $150.00"), 400, 100, TextAlignment.LEFT);

var canvas = new PdfCanvas(pdf.GetFirstPage());
canvas.MoveTo(72, 700).LineTo(524, 700).Stroke();
canvas.Rectangle(72, 600, 452, 60).Fill();
canvas.BeginText();
canvas.MoveText(80, 630);
canvas.ShowText("Item Details");
canvas.EndText();

document.Close();`;

const APRYSE_EXAMPLE = `using pdftron;
using pdftron.PDF;
using pdftron.SDF;

// Initialise the Apryse SDK (not required by Canvas.Pdf)
PDFNet.Initialize(licenseKey);

// Create a new PDF document with two pages
using var doc = new PDFDoc();

var page1 = doc.PageCreate(new Rect(0, 0, 612, 792));
doc.PagePushBack(page1);

var page2 = doc.PageCreate(new Rect(0, 0, 612, 792));
doc.PagePushBack(page2);

// Write text via ElementBuilder / ElementWriter
var builder = new ElementBuilder();
var writer  = new ElementWriter();

writer.Begin(page1);
var font = Font.Create(doc, Font.StandardType1Font.e_helvetica);
var element = builder.CreateTextBegin(font, 14);
writer.WriteElement(element);
element = builder.CreateTextRun("Hello from Apryse SDK");
element.SetTextMatrix(1, 0, 0, 1, 40, 740);
writer.WriteElement(element);
writer.WriteElement(builder.CreateTextEnd());
writer.End();

// Save with linearisation
doc.Save(outputPath, SDFDoc.SaveOptions.e_linearized);`;

const ASPOSE_EXAMPLE = `using Aspose.Pdf;
using Aspose.Pdf.Text;

var document = new Document();
var page = document.Pages.Add();

// Positioned heading via TextFragment + Position
var heading = new TextFragment("Invoice #1042");
heading.Position = new Position(40, 750);
heading.TextState.FontSize = 18;
page.Paragraphs.Add(heading);

// Simple paragraph text (no position — uses starter coordinates)
page.Paragraphs.Add(new TextFragment("Thank you for your order."));

// TextBuilder flow
var builder = new TextBuilder(page);
var note = new TextFragment("Payment due within 30 days.");
note.Position = new Position(40, 650);
builder.AppendText(note);

document.Save(outputPath);`;

const DSPDF_EXAMPLE = `using GrapeCity.Documents.Pdf;
using GrapeCity.Documents.Drawing;

var doc = new GcPdfDocument();
var page = doc.NewPage();
page.Graphics.DrawString("Invoice #2024", new TextFormat { FontSize = 18 }, new PointF(72, 72));
page.Graphics.DrawLine(pen, 72, 100, 540, 100);
page.Graphics.DrawString("Thank you for your order.", new TextFormat { FontSize = 12 }, new PointF(72, 130));
page.Graphics.DrawRectangle(pen, new RectangleF(72, 200, 468, 300));
page.Graphics.FillRectangle(brush, new RectangleF(72, 200, 468, 20));
doc.Save(outputPath);`;

const FOXIT_EXAMPLE = `using foxit;
using foxit.pdf;

Library.Initialize(licenseKey);
using var doc = new PDFDoc();
var page = doc.InsertPage(0, PageSize.e_SizeA4);
var graphics = page.GetGraphics();
graphics.DrawText("Invoice #2024", font18, 72, 72);
graphics.DrawLine(pen, 72, 100, 540, 100);
graphics.DrawText("Thank you for your order.", font12, 72, 130);
graphics.DrawRect(pen, 72, 200, 468, 300);
graphics.FillRect(brush, 72, 200, 468, 20);
page.GenerateContent();
doc.SaveAs(outputPath);`;

const DEVEXPRESS_EXAMPLE = `using DevExpress.Pdf;
using DevExpress.Drawing;

using var processor = new PdfDocumentProcessor();
processor.CreateEmptyDocument();
using var graphics = processor.CreateGraphics();

// Reusable fonts — the font size is recovered from the variable declaration.
var titleFont = new DXFont("Arial", 24);
var labelFont = new DXFont("Arial", 12);

// ---- Page 1: cover ----
// In DevExpress, draw calls come *before* RenderNewPage; the converter repositions
// them after AddPage() automatically.
graphics.DrawString("ACME Corporation", titleFont, DXBrushes.Black, 40, 760);
graphics.DrawString("Annual Invoice 2024", labelFont, DXBrushes.Blue, 40, 730);
graphics.DrawLine(new DXPen(DXColor.FromArgb(0, 102, 204), 2), 40, 715, 555, 715);
graphics.DrawString("Prepared for: Wile E. Coyote", labelFont, DXBrushes.Black, 40, 690);
graphics.DrawRectangle(DXPens.Red, 40, 600, 250, 70);
graphics.DrawRectangle(DXPens.Black, new RectangleF(320, 600, 200, 70));

processor.RenderNewPage(PdfPaperSize.A4, graphics);

// ---- Page 2: line items (second RenderNewPage reuses the page variable) ----
graphics.DrawString("Line Items", titleFont, DXBrushes.Black, 40, 760);
graphics.DrawLine(DXPens.Gray, 40, 740, 555, 740);
graphics.DrawString("1x Rocket Skates", labelFont, DXBrushes.Black, 40, 715);
graphics.DrawString("$199.00", labelFont, DXBrushes.Black, 460, 715);
graphics.DrawLine(DXPens.Gray, 40, 700, 555, 700);
graphics.DrawString("Total Due", titleFont, DXBrushes.Green, 40, 660);

processor.RenderNewPage(PdfPaperSize.A4, graphics);

// ---- Encrypt and save (maps to Canvas PdfSaveOptions.Encryption — see diagnostics) ----
var encryptionOptions = new PdfEncryptionOptions();
encryptionOptions.UserPasswordString = "open-sesame";
encryptionOptions.OwnerPasswordString = "admin";
var saveOptions = new PdfSaveOptions { EncryptionOptions = encryptionOptions };
processor.SaveDocument(outputPath, saveOptions);`;

const GEMBOX_EXAMPLE = `using GemBox.Pdf;
using GemBox.Pdf.Content;

ComponentInfo.SetLicense("FREE-LIMITED-KEY");

var doc = new PdfDocument();
var page = doc.Pages.Add();

page.Content.DrawText("Invoice #2024", new PdfPoint(72, 72));
page.Content.DrawText("Thank you for your order.", new PdfPoint(72, 130));
page.Content.DrawText("Total: $150.00", new PdfPoint(72, 160));

doc.Save(outputPath);`;

const SPIRE_EXAMPLE = `using Spire.Pdf;
using Spire.Pdf.Graphics;

var doc = new PdfDocument();
var page = doc.Pages.Add();

// Heading
page.Canvas.DrawString(
    "Invoice #2024",
    new PdfFont(PdfFontFamily.Helvetica, 18),
    PdfBrushes.Black,
    new PointF(72, 72));

// Separator line
page.Canvas.DrawLine(pen, 72, 110, 540, 110);

// Body text
page.Canvas.DrawString(
    "Thank you for your order.",
    new PdfFont(PdfFontFamily.Helvetica, 12),
    PdfBrushes.Black,
    new PointF(72, 140));

// Table outline and header fill
page.Canvas.DrawRectangle(pen, 72, 200, 468, 200);
page.Canvas.DrawRectangle(pen, new RectangleF(72, 200, 468, 24));

doc.SaveToFile(outputPath);`;

const PDFKITNET_EXAMPLE = `using PdfKitNet;

var doc = new Document();
var page = doc.NewPage();

page.DrawText("Invoice #2024", 72, 72);
page.DrawLine(72, 110, 540, 110);
page.DrawString("Thank you for your order.", 72, 140);
page.DrawRectangle(72, 200, 468, 200);

doc.Render(outputPath);`;

const LEADTOOLS_EXAMPLE = `using Leadtools.Pdf;

var doc = new PDFDocument();
var page = doc.AddPage();

page.DrawText("Invoice #2024", 72, 72);
page.DrawLine(72, 110, 540, 110);
page.DrawString("Thank you for your order.", 72, 140);
page.DrawRectangle(72, 200, 468, 200);

doc.Save(outputPath);`;

const ACTIVEPDF_EXAMPLE = `using activePDF.Toolkit;

var toolkit = new Toolkit();
var page = toolkit.AddPage();

toolkit.PrintText("Invoice #2024", 72, 72);
toolkit.DrawLine(72, 110, 540, 110);
toolkit.PrintText("Thank you for your order.", 72, 140);
toolkit.DrawRectangle(72, 200, 468, 200);

toolkit.Save(outputPath);`;

const PDFTOOLS_EXAMPLE = `using PdfTools;
using PdfTools.Pdf;

Sdk.Initialize(licenseKey);
using var input = File.OpenRead(inputPath);
using var document = Document.Open(input, null);

document.Save(outputPath);`;

const PDFTOOLS_TOOLBOX_EXAMPLE = `using PdfTools.Toolbox.Pdf;
using PdfTools.Toolbox.Pdf.Content;
using PdfTools.Toolbox.Pdf.Content.Text;

using var outStream = new FileStream(outPath, FileMode.CreateNew, FileAccess.ReadWrite);
using var outDoc = Document.Create(outStream, null, null);

var font = Font.CreateFromSystem(outDoc, "Arial", "Italic", true);
var outPage = Page.Create(outDoc, PageSize.A4);
using var gen = new ContentGenerator(outPage.Content, false);

var text = Text.Create(outDoc);
using var textGenerator = new TextGenerator(text, font, 20, null);
textGenerator.MoveTo(new Point { X = 72, Y = outPage.Size.Height - 72 });
textGenerator.ShowLine("Invoice #2024");
gen.PaintText(text);

outDoc.Pages.Add(outPage);`;

const IRONPDF_EXAMPLE = `using IronPdf;

var renderer = new ChromePdfRenderer();
renderer.RenderingOptions.MarginTop = 20;
renderer.RenderingOptions.MarginBottom = 20;
var pdf = renderer.RenderHtmlAsPdf(@"
  <h1>Invoice #2024</h1>
  <p>Thank you for your order.</p>
  <p>Total: $150.00</p>
");
pdf.SaveAs(outputPath);`;

const DEVEXPRESS_REPORT_EXAMPLE = `using DevExpress.XtraReports.UI;
using System.Drawing;

public partial class InvoiceReport : XtraReport
{
    private ReportHeaderBand ReportHeader;
    private DetailBand Detail;
    private XRLabel xrTitle;
    private XRLine xrRule;
    private XRLabel xrBody;

    private void InitializeComponent()
    {
        this.ReportHeader = new ReportHeaderBand();
        this.Detail = new DetailBand();
        this.xrTitle = new XRLabel();
        this.xrRule = new XRLine();
        this.xrBody = new XRLabel();

        this.ReportHeader.HeightF = 120F;
        this.Detail.HeightF = 400F;

        this.xrTitle.Text = "Invoice #2024-117";
        this.xrTitle.LocationF = new PointF(50F, 25F);
        this.xrTitle.SizeF = new SizeF(500F, 45F);
        this.xrTitle.Font = new Font("Tahoma", 22F, FontStyle.Bold);
        this.xrTitle.ForeColor = Color.FromArgb(0, 102, 204);
        this.xrTitle.TextAlignment = TextAlignment.MiddleLeft;

        this.xrRule.LocationF = new PointF(50F, 90F);
        this.xrRule.SizeF = new SizeF(500F, 3F);
        this.xrRule.ForeColor = Color.Gray;

        this.xrBody.Text = "Thank you for your business. Payment is due within 30 days.";
        this.xrBody.LocationF = new PointF(50F, 40F);
        this.xrBody.SizeF = new SizeF(500F, 30F);
        this.xrBody.Font = new Font("Tahoma", 11F);

        this.ReportHeader.Controls.AddRange(new XRControl[] { this.xrTitle, this.xrRule });
        this.Detail.Controls.AddRange(new XRControl[] { this.xrBody });
        this.Bands.AddRange(new Band[] { this.ReportHeader, this.Detail });
    }
}`;

const REPORT_FRAMEWORK: Framework = {
  id: REPORT_ID,
  name: 'DevExpress Reports',
  status: 'designer',
  description: 'Converts a DevExpress XtraReport — a C# class or a .repx XML layout — into an editable Canvas design (bands flattened, report units → points). Open the result in the visual designer.',
};

const RDL_REPORT_EXAMPLE = `<?xml version="1.0" encoding="utf-8"?>
<Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition" Name="Invoice">
  <Body>
    <ReportItems>
      <Textbox Name="customer">
        <Top>0in</Top><Left>1in</Left><Height>0.3in</Height><Width>3in</Width>
        <Paragraphs><Paragraph><TextRuns><TextRun><Value>=Fields!CustomerName.Value</Value></TextRun></TextRuns></Paragraph></Paragraphs>
      </Textbox>
      <Tablix Name="items">
        <Top>0.6in</Top><Left>1in</Left><Height>1in</Height><Width>4in</Width>
        <TablixBody>
          <TablixColumns>
            <TablixColumn><Width>2in</Width></TablixColumn>
            <TablixColumn><Width>2in</Width></TablixColumn>
          </TablixColumns>
          <TablixRows>
            <TablixRow><TablixCells>
              <TablixCell><CellContents><Textbox Name="h1"><Paragraphs><Paragraph><TextRuns><TextRun><Value>Item</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox></CellContents></TablixCell>
              <TablixCell><CellContents><Textbox Name="h2"><Paragraphs><Paragraph><TextRuns><TextRun><Value>Total</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox></CellContents></TablixCell>
            </TablixCells></TablixRow>
          </TablixRows>
        </TablixBody>
      </Tablix>
    </ReportItems>
    <Height>5in</Height>
  </Body>
  <Page>
    <PageHeader><Height>1in</Height><ReportItems>
      <Textbox Name="title">
        <Top>0.1in</Top><Left>1in</Left><Height>0.4in</Height><Width>5in</Width>
        <Paragraphs><Paragraph><Style><TextAlign>Center</TextAlign></Style><TextRuns><TextRun><Value>Invoice 2024</Value><Style><FontFamily>Arial</FontFamily><FontSize>20pt</FontSize><FontWeight>Bold</FontWeight><Color>#0066CC</Color></Style></TextRun></TextRuns></Paragraph></Paragraphs>
      </Textbox>
    </ReportItems></PageHeader>
    <PageFooter><Height>0.5in</Height><ReportItems>
      <Textbox Name="pageinfo">
        <Top>0.1in</Top><Left>4in</Left><Height>0.3in</Height><Width>1in</Width>
        <Paragraphs><Paragraph><TextRuns><TextRun><Value>Page 1</Value></TextRun></TextRuns></Paragraph></Paragraphs>
      </Textbox>
    </ReportItems></PageFooter>
    <PageHeight>11in</PageHeight>
    <PageWidth>8.5in</PageWidth>
    <LeftMargin>1in</LeftMargin>
    <RightMargin>1in</RightMargin>
    <TopMargin>1in</TopMargin>
    <BottomMargin>1in</BottomMargin>
  </Page>
</Report>`;

const RDL_REPORT_FRAMEWORK: Framework = {
  id: RDL_REPORT_ID,
  name: 'Syncfusion / RDL Reports',
  status: 'designer',
  description: 'Converts an RDL/RDLC report (SSRS, Syncfusion) into an editable Canvas design — items positioned absolutely, CSS lengths → points, tablix → table, page header/footer → shared elements. Open the result in the visual designer.',
};

const RPX_REPORT_EXAMPLE = `<?xml version="1.0" encoding="utf-8"?>
<Report Name="Invoice">
  <Sections>
    <PageHeader Name="PageHeader1" Height="1">
      <Controls>
        <Label Name="title" Left="1" Top="0.1" Width="5" Height="0.4" Text="Invoice 2024" Font-FamilyName="Arial" Font-Size="20" Font-Bold="True" Alignment="Center" ForeColor="0, 102, 204" />
      </Controls>
    </PageHeader>
    <Detail Name="Detail1" Height="2">
      <Controls>
        <TextBox Name="customer" Left="1" Top="0" Width="3" Height="0.3" DataField="CustomerName" />
        <Line Name="rule" X1="1" Y1="0.5" X2="6" Y2="0.5" LineWeight="2" LineColor="Gray" />
        <Barcode Name="sku" Left="1" Top="1" Width="2" Height="0.5" DataField="Sku" Style="Code128" />
      </Controls>
    </Detail>
    <PageFooter Name="PageFooter1" Height="0.5">
      <Controls>
        <Label Name="pageinfo" Left="5" Top="0.1" Width="1" Height="0.2" Text="Page 1" />
      </Controls>
    </PageFooter>
  </Sections>
</Report>`;

const RPX_REPORT_FRAMEWORK: Framework = {
  id: RPX_REPORT_ID,
  name: 'ActiveReports (.rpx)',
  status: 'designer',
  description: 'Converts a GrapeCity/MESCIUS ActiveReports section report (.rpx) into an editable Canvas design — banded sections flattened to absolute positions (inches → points), page header/footer → shared elements, DataField → binding. Open the result in the visual designer.',
};

const FRX_REPORT_EXAMPLE = `<?xml version="1.0" encoding="utf-8"?>
<Report ScriptLanguage="CSharp" ReportInfo.Name="Invoice">
  <Dictionary>
    <TableDataSource Name="Items"><Column Name="Name" DataType="System.String"/></TableDataSource>
  </Dictionary>
  <ReportPage Name="Page1">
    <ReportTitleBand Name="ReportTitle1" Top="0" Width="718.2" Height="37.8">
      <TextObject Name="title" Left="0" Top="0" Width="718.2" Height="37.8" Text="INVOICE" HorzAlign="Center" Font="Tahoma, 14pt, style=Bold" TextFill.Color="Blue" Fill.Color="WhiteSmoke"/>
    </ReportTitleBand>
    <DataBand Name="Data1" Top="64" Width="718.2" Height="40" DataSource="Items">
      <TextObject Name="name" Left="0" Top="0" Width="200" Height="20" Text="[Items.Name]" Font="Tahoma, 9pt"/>
      <LineObject Name="rule" Left="0" Top="22" Width="718.2" Height="0" Border.Color="Gray" Border.Width="2"/>
      <BarcodeObject Name="bc" Left="500" Top="0" Width="150" Height="40" Text="ABC-12345" Barcode="Code128"/>
    </DataBand>
    <PageFooterBand Name="PageFooter1" Top="120" Width="718.2" Height="20">
      <TextObject Name="pageinfo" Left="600" Top="0" Width="100" Height="20" Text="Page 1"/>
    </PageFooterBand>
  </ReportPage>
</Report>`;

const FRX_REPORT_FRAMEWORK: Framework = {
  id: FRX_REPORT_ID,
  name: 'FastReport (.frx)',
  status: 'designer',
  description: 'Converts a FastReport .NET report (.frx) into an editable Canvas design — banded layout flattened to absolute positions (pixels → points, page size in mm), page header/footer → shared elements, [Source.Column] → binding. Open the result in the visual designer.',
};

const EXAMPLES: Record<string, string> = {
  [REPORT_ID]: DEVEXPRESS_REPORT_EXAMPLE,
  [RDL_REPORT_ID]: RDL_REPORT_EXAMPLE,
  [RPX_REPORT_ID]: RPX_REPORT_EXAMPLE,
  [FRX_REPORT_ID]: FRX_REPORT_EXAMPLE,
  Syncfusion: SYNCFUSION_EXAMPLE,
  iText7: ITEXT7_EXAMPLE,
  Apryse: APRYSE_EXAMPLE,
  Aspose: ASPOSE_EXAMPLE,
  DsPdf: DSPDF_EXAMPLE,
  Foxit: FOXIT_EXAMPLE,
  DevExpress: DEVEXPRESS_EXAMPLE,
  IronPdf: IRONPDF_EXAMPLE,
  Spire: SPIRE_EXAMPLE,
  GemBox: GEMBOX_EXAMPLE,
  PdfKitNet: PDFKITNET_EXAMPLE,
  Leadtools: LEADTOOLS_EXAMPLE,
  ActivePdf: ACTIVEPDF_EXAMPLE,
  PdfTools: PDFTOOLS_EXAMPLE,
  PdfToolsToolbox: PDFTOOLS_TOOLBOX_EXAMPLE,
};

interface ConversionSummary {
  convertedCount: number;
  warningCount: number;
  errorCount: number;
  totalDiagnostics: number;
}

const MigrationsPage: React.FC = () => {
  const [frameworks, setFrameworks] = useState<Framework[]>([...FRAMEWORKS_FALLBACK, REPORT_FRAMEWORK, RDL_REPORT_FRAMEWORK, RPX_REPORT_FRAMEWORK, FRX_REPORT_FRAMEWORK]);
  const [selectedId, setSelectedId] = useState('Syncfusion');
  const [reportDesign, setReportDesign] = useState<any | null>(null);
  const navigate = useNavigate();
  const bulkReplaceContent = useEditorStore(s => s.bulkReplaceContent);
  const [sourceCode, setSourceCode] = useState('');
  const [canvasCode, setCanvasCode] = useState('');
  const [diagnostics, setDiagnostics] = useState<Diagnostic[]>([]);
  const [summary, setSummary] = useState<ConversionSummary | null>(null);
  const [hasConverted, setHasConverted] = useState(false);
  const [diagOpen, setDiagOpen] = useState(false);
  const [pdfUrl, setPdfUrl] = useState<string | null>(null);
  const [converting, setConverting] = useState(false);
  const [previewing, setPreviewing] = useState(false);
  const [copyLabel, setCopyLabel] = useState('Copy');
  const [error, setError] = useState<string | null>(null);
  const [diffMode, setDiffMode] = useState(false);
  const [splitPercent, setSplitPercent] = useState(50);
  const prevPdfUrl = useRef<string | null>(null);
  const handleConvertRef = useRef<() => Promise<void>>(async () => {});
  const dragState = useRef<{ startX: number; startPct: number } | null>(null);
  const splitRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    fetch(`${API_BASE}/frameworks`)
      .then(r => r.json())
      .then((data: Framework[]) => setFrameworks([...data, REPORT_FRAMEWORK, RDL_REPORT_FRAMEWORK, RPX_REPORT_FRAMEWORK, FRX_REPORT_FRAMEWORK]))
      .catch(() => { /* use fallback */ });
    return () => { if (prevPdfUrl.current) URL.revokeObjectURL(prevPdfUrl.current); };
  }, []);

  const current = frameworks.find(f => f.id === selectedId);

  const handleFrameworkChange = (id: string) => {
    setSelectedId(id);
    setSourceCode('');
    setCanvasCode('');
    setDiagnostics([]);
    setSummary(null);
    setHasConverted(false);
    setDiagOpen(false);
    setPdfUrl(null);
    setError(null);
    setReportDesign(null);
  };

  const applyConvertResult = (data: { canvasCode?: string; diagnostics?: Diagnostic[]; summary?: ConversionSummary }) => {
    const diags = data.diagnostics ?? [];
    setCanvasCode(data.canvasCode ?? '');
    setDiagnostics(diags);
    setSummary(data.summary ?? null);
    setHasConverted(true);
    setDiagOpen(diags.some(d => d.severity === 'Warning'));
  };

  const handleConvert = async () => {
    if (!sourceCode.trim()) return;
    setConverting(true);
    setError(null);
    try {
      // Report (DevExpress XtraReport or RDL/RDLC) → Canvas design (JSON), opened in the visual designer.
      if (isReportDesign(selectedId)) {
        const res = await fetch(`${API_BASE}/report-to-design`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ sourceCode }),
        });
        if (!res.ok) { const e = await res.json(); throw new Error(e.error ?? `HTTP ${res.status}`); }
        const data = await res.json();
        const diags: Diagnostic[] = data.diagnostics ?? [];
        const elementCount = data.design?.pages?.[0]?.elements?.length ?? 0;
        setReportDesign(data.design);
        setCanvasCode(JSON.stringify(data.design, null, 2));
        setDiagnostics(diags);
        setSummary({
          convertedCount: elementCount,
          warningCount: diags.filter(d => d.severity === 'Warning').length,
          errorCount: diags.filter(d => d.severity === 'Error').length,
          totalDiagnostics: diags.length,
        });
        setHasConverted(true);
        setDiagOpen(diags.some(d => d.severity === 'Warning'));
        return;
      }

      const res = await fetch(`${API_BASE}/convert`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ framework: selectedId, sourceCode }),
      });
      if (!res.ok) { const e = await res.json(); throw new Error(e.error ?? `HTTP ${res.status}`); }
      applyConvertResult(await res.json());
    } catch (e: any) {
      setError(e.message ?? 'Conversion failed — is the Canvas.WebApi backend running on port 5086?');
    } finally {
      setConverting(false);
    }
  };

  const handleOpenInDesigner = () => {
    if (!reportDesign) return;
    const pages = (reportDesign.pages ?? []).map((p: any) => ({ id: p.id, elements: p.elements ?? [] }));
    bulkReplaceContent(pages.length ? pages : [{ id: 'page-1', elements: [] }], reportDesign.sharedElements ?? []);
    navigate('/create');
  };

  const handlePreview = async () => {
    if (!sourceCode.trim()) return;
    setPreviewing(true);
    setError(null);
    try {
      // Sync the output panel first so the user sees the converted code
      const convertRes = await fetch(`${API_BASE}/convert`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ framework: selectedId, sourceCode }),
      });
      if (convertRes.ok) applyConvertResult(await convertRes.json());

      const res = await fetch(`${API_BASE}/preview`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ framework: selectedId, sourceCode }),
      });
      if (!res.ok) { const e = await res.json(); throw new Error(e.error ?? `HTTP ${res.status}`); }
      const blob = await res.blob();
      if (prevPdfUrl.current) URL.revokeObjectURL(prevPdfUrl.current);
      const url = URL.createObjectURL(blob);
      prevPdfUrl.current = url;
      setPdfUrl(url);
    } catch (e: any) {
      setError(e.message ?? 'Preview failed — is the Canvas.WebApi backend running on port 5086?');
    } finally {
      setPreviewing(false);
    }
  };

  const handleCopy = async () => {
    if (!canvasCode) return;
    await navigator.clipboard.writeText(canvasCode);
    setCopyLabel('Copied!');
    setTimeout(() => setCopyLabel('Copy'), 2000);
  };

  const handleDownload = () => {
    if (!canvasCode) return;
    const blob = new Blob([canvasCode], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'migration.cs';
    a.click();
    URL.revokeObjectURL(url);
  };

  const handleDragStart = useCallback((e: React.MouseEvent) => {
    e.preventDefault();
    dragState.current = { startX: e.clientX, startPct: splitPercent };
    const onMove = (ev: MouseEvent) => {
      if (!dragState.current || !splitRef.current) return;
      const containerWidth = splitRef.current.offsetWidth;
      const delta = ev.clientX - dragState.current.startX;
      const newPct = Math.min(80, Math.max(20, dragState.current.startPct + (delta / containerWidth) * 100));
      setSplitPercent(newPct);
    };
    const onUp = () => {
      dragState.current = null;
      window.removeEventListener('mousemove', onMove);
      window.removeEventListener('mouseup', onUp);
    };
    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', onUp);
  }, [splitPercent]);

  const warnCount = diagnostics.filter(d => d.severity === 'Warning').length;

  // Keep ref current so the Monaco command closure never goes stale
  handleConvertRef.current = handleConvert;

  const handleSourceMount: OnMount = useCallback((editor, monaco) => {
    editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.Enter, () => {
      void handleConvertRef.current();
    });
  }, []);

  return (
    <div className="mgr-page">
      <AppHeader activePage="migrations" />

      <main className="mgr-main">
        {/* Page heading */}
        <div className="mgr-heading">
          <div className="mgr-heading-left">
            <FiCode className="mgr-heading-icon" />
            <div>
              <h1>Code Migrations</h1>
              <p>Paste code from another PDF library, convert it to Canvas.Pdf, and preview the result instantly.</p>
            </div>
          </div>
        </div>

        {/* Framework selector */}
        <div className="mgr-framework-bar">
          <label htmlFor="mgr-fw-select">Source framework</label>
          <select
            id="mgr-fw-select"
            value={selectedId}
            onChange={e => handleFrameworkChange(e.target.value)}
          >
            {frameworks.map(f => (
              <option key={f.id} value={f.id}>
                {f.name}{f.status === 'skeleton' ? ' (skeleton)' : f.status === 'pilot' ? ' (pilot)' : ''}
              </option>
            ))}
          </select>
          {current && (
            <span className="mgr-framework-desc">{current.description}</span>
          )}
          {current?.status === 'full' && (
            <span className="mgr-badge mgr-badge-full">Full</span>
          )}
          {current?.status === 'pilot' && (
            <span className="mgr-badge mgr-badge-pilot">Pilot</span>
          )}
          {current?.status === 'skeleton' && (
            <span className="mgr-badge mgr-badge-skeleton">Skeleton</span>
          )}
          {EXAMPLES[selectedId] && (
            <button
              className="mgr-example-btn"
              onClick={() => setSourceCode(EXAMPLES[selectedId])}
              title={`Load ${current?.name ?? selectedId} example`}
            >
              Load example
            </button>
          )}
        </div>

        {error && (
          <div className="mgr-error" role="alert">{error}</div>
        )}

        {/* Split: source | canvas */}
        <div className="mgr-split" ref={splitRef}>
          <div className="mgr-pane" style={{ width: `${splitPercent}%` }}>
            <div className="mgr-pane-header">
              <span>Source Code — {current?.name ?? selectedId}</span>
            </div>
            <div className="mgr-editor-wrapper">
              <Editor
                language="csharp"
                value={sourceCode}
                onChange={v => setSourceCode(v ?? '')}
                onMount={handleSourceMount}
                options={{
                  minimap: { enabled: false },
                  scrollBeyondLastLine: false,
                  fontSize: 13,
                  lineNumbers: 'on',
                  wordWrap: 'on',
                  renderWhitespace: 'none',
                  padding: { top: 8, bottom: 8 },
                }}
                height="100%"
              />
            </div>
            <div className="mgr-pane-footer">
              <button
                className="mgr-btn mgr-btn-primary"
                onClick={handleConvert}
                disabled={converting || !sourceCode.trim()}
              >
                {converting
                  ? <><FiRefreshCw className="mgr-spin" /> Converting…</>
                  : <>Convert <span className="mgr-arrow">→</span></>}
              </button>
            </div>
          </div>

          <div className="mgr-drag-handle" onMouseDown={handleDragStart} title="Drag to resize" />

          <div className="mgr-pane" style={{ flex: 1 }}>
            <div className="mgr-pane-header">
              <span>{isReportDesign(selectedId) ? 'Canvas Design (JSON)' : 'Canvas.Pdf Code'}</span>
              <div className="mgr-pane-header-actions">
                {hasConverted && (
                  <button
                    className={`mgr-icon-btn${diffMode ? ' mgr-icon-btn-active' : ''}`}
                    onClick={() => setDiffMode(d => !d)}
                    title="Toggle diff view"
                  >
                    <FiGitMerge /> Diff
                  </button>
                )}
                <button className="mgr-icon-btn" onClick={handleCopy} disabled={!canvasCode} title="Copy to clipboard">
                  <FiCopy /> {copyLabel}
                </button>
                <button className="mgr-icon-btn" onClick={handleDownload} disabled={!canvasCode} title="Download as .cs file">
                  <FiDownload /> Download .cs
                </button>
              </div>
            </div>
            <div className="mgr-editor-wrapper">
              {diffMode && canvasCode ? (
                <DiffEditor
                  language="csharp"
                  original={sourceCode}
                  modified={canvasCode}
                  options={{
                    readOnly: true,
                    minimap: { enabled: false },
                    scrollBeyondLastLine: false,
                    fontSize: 13,
                    wordWrap: 'on',
                    renderWhitespace: 'none',
                    padding: { top: 8, bottom: 8 },
                    renderSideBySide: true,
                    originalEditable: false,
                  }}
                  height="100%"
                />
              ) : (
                <Editor
                  language={isReportDesign(selectedId) ? 'json' : 'csharp'}
                  value={canvasCode}
                  options={{
                    readOnly: true,
                    minimap: { enabled: false },
                    scrollBeyondLastLine: false,
                    fontSize: 13,
                    lineNumbers: 'on',
                    wordWrap: 'on',
                    renderWhitespace: 'none',
                    padding: { top: 8, bottom: 8 },
                  }}
                  height="100%"
                />
              )}
            </div>
            <div className="mgr-pane-footer mgr-pane-footer-right">
              {isReportDesign(selectedId) ? (
                <button
                  className="mgr-btn mgr-btn-primary"
                  onClick={handleOpenInDesigner}
                  disabled={!reportDesign}
                  title="Load the converted report into the visual designer"
                >
                  <FiLayout /> Open in Designer
                </button>
              ) : (
                <button
                  className="mgr-btn mgr-btn-secondary"
                  onClick={handlePreview}
                  disabled={previewing || !sourceCode.trim()}
                >
                  {previewing
                    ? <><FiRefreshCw className="mgr-spin" /> Generating…</>
                    : <><FiPlay /> Generate Preview</>}
                </button>
              )}
            </div>
          </div>
        </div>

        {/* Diagnostics — always visible after a conversion */}
        {hasConverted && (
          <div className="mgr-diagnostics">
            <div className="mgr-diag-summary">
              <strong>Diagnostics</strong>
              {summary && summary.convertedCount > 0 && (
                <span className="mgr-diag-chip mgr-diag-chip-converted">✓ {summary.convertedCount} converted</span>
              )}
              {warnCount > 0 && (
                <span className="mgr-diag-chip mgr-diag-chip-warn">⚠ {warnCount} warning{warnCount > 1 ? 's' : ''}</span>
              )}
              {diagnostics.length === 0 && (
                <span className="mgr-diag-chip mgr-diag-chip-ok">No issues</span>
              )}
              {diagnostics.length > 0 && (
                <button
                  className="mgr-diag-toggle"
                  onClick={() => setDiagOpen(o => !o)}
                  aria-expanded={diagOpen}
                >
                  {diagOpen ? '▲ Hide' : '▼ Show'} details
                </button>
              )}
            </div>
            {diagOpen && (
              <ul className="mgr-diag-list">
                {diagnostics.map((d, i) => (
                  <li key={i} className={`mgr-diag-item mgr-diag-${d.severity.toLowerCase()}`}>
                    <code className="mgr-diag-code">{d.code}</code>
                    <span>{d.message}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}

        {/* PDF Preview */}
        <div className="mgr-preview">
          <div className="mgr-preview-header">
            <span>PDF Preview</span>
          </div>
          {pdfUrl
            ? <iframe className="mgr-pdf-frame" src={pdfUrl} title="PDF Preview" />
            : (
              <div className="mgr-pdf-empty">
                <FiPlay size={32} />
                <p>Click <strong>Generate Preview</strong> to render the converted PDF</p>
              </div>
            )
          }
        </div>
      </main>
    </div>
  );
};

export default MigrationsPage;
