import React, { useState, useEffect, useRef, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { FiCode, FiCopy, FiDownload, FiExternalLink, FiPlay, FiRefreshCw, FiGitMerge, FiLayout, FiUpload } from 'react-icons/fi';
import Editor, { DiffEditor, type OnMount } from '@monaco-editor/react';
import AppHeader from '@/components/Layout/AppHeader';
import MigrationTabs, { pdfTabs, sheetTabs } from '@/components/Migrations/MigrationTabs';
import { blobToDataUrl, writePdfViewerHandoff } from '@/features/pdf-viewer/handoff';
import { DEFAULT_PAGE_SETTINGS, normalizePageSettings, useEditorStore, type Template } from '@/store';

// Framework ids for the report → PXA Designer flows (output is a design, not C# code).
const REPORT_ID = 'DevExpressReport';
const RDL_REPORT_ID = 'RdlReport';
const RPX_REPORT_ID = 'RpxReport';
const FRX_REPORT_ID = 'FrxReport';
const TRDX_REPORT_ID = 'TrdxReport';
const JRXML_REPORT_ID = 'JrxmlReport';
const ACTIVE_REPORTS_JS_ID = 'ActiveReportsJsReport';
const MRT_REPORT_ID = 'MrtReport';
// All report flows post to the same /report-to-design endpoint (the backend auto-detects the format).
const isReportDesign = (id: string) =>
  id === REPORT_ID || id === RDL_REPORT_ID || id === RPX_REPORT_ID || id === FRX_REPORT_ID
  || id === TRDX_REPORT_ID || id === JRXML_REPORT_ID || id === ACTIVE_REPORTS_JS_ID || id === MRT_REPORT_ID;

interface Framework {
  id: string;
  name: string;
  status: string;
  description: string;
  kind?: string; // "pdf" (default) | "spreadsheet"
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
  { id: 'IronPdf',    name: 'IronPDF',           status: 'pilot',   description: 'Roslyn-based pilot: ChromePdfRenderer → PdfDocument + AddPage scaffold; SaveAs → document.Save(); HTML/URL/Razor rendering calls replaced with diagnostics for manual PXA draw call migration.' },
  { id: 'Spire',      name: 'Spire.PDF',         status: 'full',    description: 'Roslyn-based full conversion: PdfDocument + Pages.Add → AddPage; page.Canvas.DrawString → DrawTextFromTop; page.Canvas.DrawLine → DrawLineFromTop; page.Canvas.DrawRectangle/FillRectangle → DrawRectangleFromTop; SaveToFile → Save; tables/forms/annotations produce warnings.' },
  { id: 'GemBox',     name: 'GemBox.Pdf',        status: 'full',    description: 'Roslyn-based full conversion: PdfDocument + Pages.Add → AddPage; Content.DrawText → DrawTextFromTop; Content.DrawLine → DrawLineFromTop; Content.DrawRectangle → DrawRectangleFromTop; ComponentInfo.SetLicense removed; forms/encryption/annotations produce warnings.' },
  { id: 'PdfKitNet',  name: 'PDFKit.NET',        status: 'full',    description: 'Roslyn-based full conversion: Document + NewPage/Pages.Add → AddPage; DrawText/DrawString → DrawTextFromTop; DrawLine → DrawLineFromTop; DrawRectangle → DrawRectangleFromTop; Save/Render → Save; forms/encryption/annotations produce warnings. Package identity must be manually verified.' },
  { id: 'Leadtools',  name: 'LEADTOOLS',         status: 'full',    description: 'Roslyn-based full conversion: PDFDocument + AddPage/Pages.Add → AddPage; DrawText/DrawString → DrawTextFromTop; DrawLine → DrawLineFromTop; DrawRectangle → DrawRectangleFromTop; Save/Export → Save; raster/OCR/barcode/conversion APIs produce warnings.' },
  { id: 'ActivePdf',  name: 'ActivePDF',         status: 'pilot',   description: 'Cautious Roslyn pilot for likely Toolkit-style generation; DocConverter, WebGrabber, COM/server, printer, merge, and stamp workflows are manual.' },
  { id: 'PdfTools',   name: 'PDFTools / Pdftools SDK', status: 'pilot', description: 'Cautious Roslyn pilot: removes Sdk.Initialize and flags SDK conversion/processing workflows for manual PXA-compatible PDF migration. Direct PDF generation belongs to the separate PDF Toolbox SDK/add-on.' },
  { id: 'PdfToolsToolbox', name: 'PDF Toolbox SDK', status: 'pilot', description: 'Cautious Roslyn pilot for Toolbox direct-generation flows: Document.Create/Page.Create/TextGenerator.ShowLine → PXA-compatible PDF code; existing-PDF editing and rich styling remain manual.' },
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

// Initialise the Apryse SDK (not required by PXA-compatible PDF output)
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

// ---- Encrypt and save (maps to PXA PdfSaveOptions.Encryption — see diagnostics) ----
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
  description: 'Converts a DevExpress XtraReport — a C# class or a .repx XML layout — into an editable PXA design (bands flattened, report units → points). Open the result in the visual designer.',
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
  description: 'Converts an RDL/RDLC report (SSRS, Syncfusion) into an editable PXA design — items positioned absolutely, CSS lengths → points, tablix → table, page header/footer → shared elements. Open the result in the visual designer.',
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
  description: 'Converts a GrapeCity/MESCIUS ActiveReports section report (.rpx) into an editable PXA design — banded sections flattened to absolute positions (inches → points), page header/footer → shared elements, DataField → binding. Open the result in the visual designer.',
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
  description: 'Converts a FastReport .NET report (.frx) into an editable PXA design — banded layout flattened to absolute positions (pixels → points, page size in mm), page header/footer → shared elements, [Source.Column] → binding. Open the result in the visual designer.',
};

const TRDX_REPORT_EXAMPLE = `<?xml version="1.0" encoding="utf-8"?>
<Report Width="8.1in" Name="Invoice" xmlns="http://schemas.telerik.com/reporting/2012/3.6">
  <PageSettings><PaperKind>Letter</PaperKind><Margins Left="1in" Right="1in" Top="1in" Bottom="1in"/></PageSettings>
  <Items>
    <PageHeaderSection Height="0.5in" Name="pageHeaderSection1">
      <Items>
        <TextBox Width="3.5in" Height="0.3in" Left="0in" Top="0.1in" Value="INVOICE" Name="title" StyleName="Header">
          <Style TextAlign="Center" Color="0, 102, 204"/>
        </TextBox>
      </Items>
    </PageHeaderSection>
    <DetailSection Height="1in" Name="detailSection1">
      <Items>
        <TextBox Width="3in" Height="0.3in" Left="0in" Top="0in" Value="=Fields.CustomerName" Name="customer"/>
        <Barcode Width="2in" Height="0.5in" Left="0in" Top="0.4in" Value="ABC-12345" Type="Code128" Name="bc"/>
      </Items>
    </DetailSection>
    <PageFooterSection Height="0.4in" Name="pageFooterSection1">
      <Items>
        <TextBox Width="1in" Height="0.2in" Left="6in" Top="0in" Value="Page 1" Name="pageinfo"/>
      </Items>
    </PageFooterSection>
  </Items>
  <StyleSheet>
    <StyleRule>
      <Style><Font Name="Segoe UI" Size="20pt" Bold="True"/></Style>
      <Selectors><StyleSelector Type="ReportItemBase" StyleName="Header"/></Selectors>
    </StyleRule>
  </StyleSheet>
</Report>`;

const TRDX_REPORT_FRAMEWORK: Framework = {
  id: TRDX_REPORT_ID,
  name: 'Telerik Reporting (.trdx)',
  status: 'designer',
  description: 'Converts a Telerik Reporting report (.trdx) into an editable PXA design — sections flattened to absolute positions (Unit strings → points), named StyleSheet styles resolved, page header/footer → shared elements, =Fields.X → binding. Open the result in the visual designer.',
};

const JRXML_REPORT_EXAMPLE = `<?xml version="1.0" encoding="UTF-8"?>
<jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports" name="Invoice"
    pageWidth="595" pageHeight="842" columnWidth="555" leftMargin="20" rightMargin="20" topMargin="20" bottomMargin="20">
  <style name="Header" forecolor="#0066CC"><font fontName="Arial" size="20" isBold="true"/></style>
  <field name="customerName" class="java.lang.String"/>
  <title>
    <band height="40">
      <staticText>
        <reportElement key="title" x="0" y="0" width="555" height="30" style="Header"/>
        <textElement textAlignment="Center"/>
        <text><![CDATA[INVOICE]]></text>
      </staticText>
    </band>
  </title>
  <detail>
    <band height="40">
      <textField>
        <reportElement key="customer" x="0" y="0" width="200" height="20"/>
        <textElement/>
        <textFieldExpression><![CDATA[$F{customerName}]]></textFieldExpression>
      </textField>
      <line>
        <reportElement key="rule" x="0" y="25" width="555" height="1" forecolor="#808080"/>
        <graphicElement><pen lineWidth="2"/></graphicElement>
      </line>
    </band>
  </detail>
  <pageFooter>
    <band height="20">
      <staticText><reportElement key="pageinfo" x="500" y="0" width="55" height="20"/><text><![CDATA[Page 1]]></text></staticText>
    </band>
  </pageFooter>
</jasperReport>`;

const JRXML_REPORT_FRAMEWORK: Framework = {
  id: JRXML_REPORT_ID,
  name: 'JasperReports (.jrxml)',
  status: 'designer',
  description: 'Converts a JasperReports / Jaspersoft Studio report (.jrxml) into an editable PXA design — bands flattened to absolute positions (points, no scaling), named styles resolved, page header/footer → shared elements, $F{field} → binding. Open the result in the visual designer.',
};

const ACTIVE_REPORTS_JS_EXAMPLE = `{
  "reportType": "ActiveReportsJS",
  "name": "Invoice JS",
  "page": { "width": "8.5in", "height": "11in" },
  "body": {
    "reportItems": [
      {
        "type": "textbox",
        "name": "title",
        "left": "1in",
        "top": "0.5in",
        "width": "4in",
        "height": "0.4in",
        "value": "Invoice",
        "style": { "fontFamily": "Arial", "fontSize": 18, "bold": true, "textAlign": "Center", "color": "#0066CC" }
      },
      { "type": "textbox", "name": "customer", "left": 72, "top": 90, "width": 220, "height": 20, "value": "{Customers.Name}" },
      { "type": "line", "name": "rule", "left": 72, "top": 120, "width": 420, "height": 1, "style": { "color": "#808080", "strokeWidth": 2 } },
      {
        "type": "table",
        "name": "items",
        "left": 72,
        "top": 150,
        "width": 320,
        "height": 80,
        "columns": [{ "width": 180 }, { "width": 140 }],
        "rows": [["Item", "Amount"], ["Widget", "{Items.Amount}"]]
      }
    ]
  }
}`;

const ACTIVE_REPORTS_JS_FRAMEWORK: Framework = {
  id: ACTIVE_REPORTS_JS_ID,
  name: 'ActiveReports JS (.json)',
  status: 'designer',
  description: 'Converts marked ActiveReports JS JSON reports into editable PXA designs — text, line, image, barcode and simple table items map directly; unknown regions become review placeholders.',
};

const MRT_REPORT_EXAMPLE = `<?xml version="1.0" encoding="utf-8"?>
<StiSerializer version="1.02" type="Net" application="StiReport">
  <ReportName>Invoice</ReportName>
  <Pages isList="true" count="1">
    <Page1 type="Page"><PaperSize>A4</PaperSize>
      <Components isList="true">
        <ReportTitleBand1 type="ReportTitleBand"><ClientRectangle>0,20,749,40</ClientRectangle>
          <Components isList="true">
            <Text1 type="Text"><ClientRectangle>0,0,749,40</ClientRectangle><Font>Arial,20,Bold,Point,False,0</Font>
              <HorAlignment>Center</HorAlignment><Text>INVOICE</Text><TextBrush>[0:102:204]</TextBrush><Name>Text1</Name></Text1>
          </Components><Name>ReportTitleBand1</Name>
        </ReportTitleBand1>
        <DataBand1 type="DataBand"><ClientRectangle>0,80,749,40</ClientRectangle>
          <Components isList="true">
            <Text2 type="Text"><ClientRectangle>0,0,300,20</ClientRectangle><Text>{Customers.CompanyName}</Text><Name>Text2</Name></Text2>
            <Line1 type="HorizontalLinePrimitive"><ClientRectangle>0,30,749,1</ClientRectangle><Color>[128:128:128]</Color><Name>Line1</Name></Line1>
          </Components><Name>DataBand1</Name>
        </DataBand1>
        <PageFooterBand1 type="PageFooterBand"><ClientRectangle>0,1071,749,20</ClientRectangle>
          <Components isList="true">
            <Text3 type="Text"><ClientRectangle>600,0,149,20</ClientRectangle><Text>{PageNofM}</Text><Name>Text3</Name></Text3>
          </Components><Name>PageFooterBand1</Name>
        </PageFooterBand1>
      </Components><Name>Page1</Name>
    </Page1>
  </Pages>
</StiSerializer>`;

const MRT_REPORT_FRAMEWORK: Framework = {
  id: MRT_REPORT_ID,
  name: 'Stimulsoft (.mrt)',
  status: 'designer',
  description: 'Converts a Stimulsoft Reports report (.mrt, StiSerializer XML) into an editable PXA design — bands with explicit positions flattened (hundredths-inch → points), page header/footer → shared elements, {Source.Field} → binding. Open the result in the visual designer.',
};

// ── Spreadsheet code-migration examples (library C# → PXA spreadsheet API) ──
const CLOSEDXML_SPREADSHEET_EXAMPLE = `using ClosedXML.Excel;

var workbook = new XLWorkbook();
var ws = workbook.Worksheets.Add("Sales");

ws.Cell("A1").Value = "Item";
ws.Cell("B1").Value = "Qty";
ws.Cell("C1").Value = "Price";
ws.Cell("A1").Style.Font.Bold = true;
ws.Cell("B1").Style.Font.Bold = true;
ws.Cell("C1").Style.Font.Bold = true;

ws.Cell("A2").Value = "Coffee";
ws.Cell("B2").Value = 3;
ws.Cell("C2").Value = 4.50;

ws.Cell("A3").Value = "Tea";
ws.Cell("B3").Value = 5;
ws.Cell("C3").Value = 2.75;

ws.Cell("B4").FormulaA1 = "SUM(B2:B3)";
ws.Cell("C4").Style.Fill.BackgroundColor = XLColor.Yellow;
ws.Cell("A4").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
ws.Column(1).Width = 20;

workbook.SaveAs("sales.xlsx");`;

const EPPLUS_SPREADSHEET_EXAMPLE = `using OfficeOpenXml;
using System.IO;

using var package = new ExcelPackage();
var ws = package.Workbook.Worksheets.Add("Sales");

ws.Cells["A1"].Value = "Sales Report";
ws.Cells["A1:C1"].Merge = true;
ws.Cells["A1"].Style.Font.Bold = true;
ws.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

ws.Cells["A2"].Value = "Item";
ws.Cells["B2"].Value = "Qty";
ws.Cells["C2"].Value = "Price";

ws.Cells["A3"].Value = "Coffee";
ws.Cells["B3"].Value = 3;
ws.Cells["C3"].Value = 4.50;

ws.Cells["A4"].Value = "Tea";
ws.Cells["B4"].Value = 5;
ws.Cells["C4"].Value = 2.75;

ws.Cells["B5"].Formula = "SUM(B3:B4)";
ws.Column(1).Width = 20;

package.SaveAs(new FileInfo("sales.xlsx"));`;

const GEMBOX_SPREADSHEET_EXAMPLE = `using GemBox.Spreadsheet;

SpreadsheetInfo.SetLicense("FREE-LIMITED-KEY");

var workbook = new ExcelFile();
var ws = workbook.Worksheets.Add("Sales");

ws.Cells["A1"].Value = "Item";
ws.Cells["B1"].Value = "Qty";
ws.Cells["A1"].Style.Font.Weight = ExcelFont.BoldWeight;
ws.Cells["B1"].Style.Font.Weight = ExcelFont.BoldWeight;
ws.Cells["A1"].Style.HorizontalAlignment = HorizontalAlignmentStyle.Center;

ws.Cells["A2"].Value = "Coffee";
ws.Cells["B2"].Value = 3;
ws.Cells["A3"].Value = "Tea";
ws.Cells["B3"].Value = 5;

ws.Cells["B4"].Formula = "=SUM(B2:B3)";

workbook.Save("sales.xlsx");`;

const ASPOSE_CELLS_EXAMPLE = `using Aspose.Cells;

var workbook = new Workbook();
var ws = workbook.Worksheets[0];
ws.Name = "Sales";

ws.Cells["A1"].PutValue("Item");
ws.Cells["B1"].PutValue("Qty");
ws.Cells["A2"].PutValue("Coffee");
ws.Cells["B2"].PutValue(3);
ws.Cells["A3"].PutValue("Tea");
ws.Cells["B3"].PutValue(5);

ws.Cells["B4"].Formula = "=SUM(B2:B3)";
ws.Cells.SetColumnWidth(0, 20);

workbook.Save("sales.xlsx");`;

const SPIRE_XLS_EXAMPLE = `using Spire.Xls;

var workbook = new Workbook();
var sheet = workbook.Worksheets[0];

sheet.Range["A1"].Text = "Item";
sheet.Range["B1"].Text = "Qty";
sheet.Range["A1"].Style.Font.IsBold = true;
sheet.Range["B1"].Style.Font.IsBold = true;

sheet.Range["A2"].Text = "Coffee";
sheet.Range["B2"].NumberValue = 3;
sheet.Range["A3"].Text = "Tea";
sheet.Range["B3"].NumberValue = 5;

sheet.Range["B4"].Formula = "=SUM(B2:B3)";
sheet.SetColumnWidth(1, 18);

workbook.SaveToFile("sales.xlsx", ExcelVersion.Version2013);`;

const SYNCFUSION_XLSIO_EXAMPLE = `using Syncfusion.XlsIO;

using ExcelEngine excelEngine = new ExcelEngine();
IApplication application = excelEngine.Excel;
IWorkbook workbook = application.Workbooks.Create(1);
IWorksheet worksheet = workbook.Worksheets[0];

worksheet.Range["A1"].Text = "Item";
worksheet.Range["B1"].Text = "Qty";
worksheet.Range["A1"].CellStyle.Font.Bold = true;
worksheet.Range["B1"].CellStyle.Font.Bold = true;

worksheet.Range["A2"].Text = "Coffee";
worksheet.Range["B2"].Number = 3;
worksheet.Range["A3"].Text = "Tea";
worksheet.Range["B3"].Number = 5;

worksheet.Range["B4"].Formula = "=SUM(B2:B3)";
worksheet.SetColumnWidth(1, 18);

workbook.SaveAs("sales.xlsx");`;

const NPOI_EXAMPLE = `using NPOI.XSSF.UserModel;
using NPOI.SS.UserModel;
using System.IO;

IWorkbook wb = new XSSFWorkbook();
ISheet sheet = wb.CreateSheet("Sales");

IRow header = sheet.CreateRow(0);
header.CreateCell(0).SetCellValue("Item");
header.CreateCell(1).SetCellValue("Qty");

IRow r1 = sheet.CreateRow(1);
r1.CreateCell(0).SetCellValue("Coffee");
r1.CreateCell(1).SetCellValue(3);

IRow r2 = sheet.CreateRow(2);
r2.CreateCell(0).SetCellValue("Tea");
r2.CreateCell(1).SetCellValue(5);

sheet.CreateRow(3).CreateCell(1).SetCellFormula("SUM(B1:B3)");

using (var fs = new FileStream("sales.xlsx", FileMode.Create))
    wb.Write(fs);`;

const SPREADSHEETLIGHT_EXAMPLE = `using SpreadsheetLight;

var doc = new SLDocument();

doc.SetCellValue("A1", "Item");
doc.SetCellValue("B1", "Qty");
doc.SetCellValue("A2", "Coffee");
doc.SetCellValue("B2", 3);
doc.SetCellValue("A3", "Tea");
doc.SetCellValue("B3", 5);
doc.SetCellValue("B4", "=SUM(B2:B3)");

doc.SaveAs("sales.xlsx");`;

const EXAMPLES: Record<string, string> = {
  [REPORT_ID]: DEVEXPRESS_REPORT_EXAMPLE,
  [RDL_REPORT_ID]: RDL_REPORT_EXAMPLE,
  [RPX_REPORT_ID]: RPX_REPORT_EXAMPLE,
  [FRX_REPORT_ID]: FRX_REPORT_EXAMPLE,
  [TRDX_REPORT_ID]: TRDX_REPORT_EXAMPLE,
  [JRXML_REPORT_ID]: JRXML_REPORT_EXAMPLE,
  [ACTIVE_REPORTS_JS_ID]: ACTIVE_REPORTS_JS_EXAMPLE,
  [MRT_REPORT_ID]: MRT_REPORT_EXAMPLE,
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
  // Spreadsheet code migration
  ClosedXmlSpreadsheet: CLOSEDXML_SPREADSHEET_EXAMPLE,
  EpplusSpreadsheet: EPPLUS_SPREADSHEET_EXAMPLE,
  GemBoxSpreadsheet: GEMBOX_SPREADSHEET_EXAMPLE,
  AsposeCells: ASPOSE_CELLS_EXAMPLE,
  SpireXls: SPIRE_XLS_EXAMPLE,
  SyncfusionXlsIo: SYNCFUSION_XLSIO_EXAMPLE,
  Npoi: NPOI_EXAMPLE,
  SpreadsheetLight: SPREADSHEETLIGHT_EXAMPLE,
};

interface ConversionSummary {
  convertedCount: number;
  warningCount: number;
  errorCount: number;
  totalDiagnostics: number;
}

// The report-designer → PXA design frameworks (output is an editable design, not C# code).
const DESIGNER_FRAMEWORKS: Framework[] = [
  REPORT_FRAMEWORK, RDL_REPORT_FRAMEWORK, RPX_REPORT_FRAMEWORK, FRX_REPORT_FRAMEWORK, TRDX_REPORT_FRAMEWORK, JRXML_REPORT_FRAMEWORK, ACTIVE_REPORTS_JS_FRAMEWORK, MRT_REPORT_FRAMEWORK,
];

type MigrationMode = 'code' | 'designer';

const MigrationsPage: React.FC<{ mode: MigrationMode; codeKind?: 'pdf' | 'spreadsheet' }> = ({ mode, codeKind }) => {
  const isDesigner = mode === 'designer';
  // Code Migration sub-tab (PDF | Spreadsheet) is driven by the route (codeKind prop).
  const kindFilter: 'pdf' | 'spreadsheet' = codeKind ?? 'pdf';
  const [frameworks, setFrameworks] = useState<Framework[]>(isDesigner ? DESIGNER_FRAMEWORKS : FRAMEWORKS_FALLBACK);
  const [selectedId, setSelectedId] = useState(isDesigner ? REPORT_ID : (kindFilter === 'spreadsheet' ? 'ClosedXmlSpreadsheet' : 'Syncfusion'));
  const [reportDesign, setReportDesign] = useState<any | null>(null);
  const navigate = useNavigate();
  const setCurrentTemplate = useEditorStore(s => s.setCurrentTemplate);
  const updatePageSettings = useEditorStore(s => s.updatePageSettings);
  const [sourceCode, setSourceCode] = useState('');
  // Base64 of a binary/zip report package (.trdp, packaged .rdlx); when set, sent as sourceBase64.
  const [sourceBase64, setSourceBase64] = useState<string | null>(null);
  const [binaryFileName, setBinaryFileName] = useState('');
  const [resourceJson, setResourceJson] = useState('');
  const [resourceXml, setResourceXml] = useState('');
  const [resourceFileName, setResourceFileName] = useState('');
  const [jrxmlResourceMap, setJrxmlResourceMap] = useState<Record<string, string>>({});
  const [jrxmlResourceFileNames, setJrxmlResourceFileNames] = useState<string[]>([]);
  const [rpxResourceMap, setRpxResourceMap] = useState<Record<string, string>>({});
  const [rpxResourceFileNames, setRpxResourceFileNames] = useState<string[]>([]);
  const [pxaCode, setPxaCode] = useState('');
  const [diagnostics, setDiagnostics] = useState<Diagnostic[]>([]);
  const [summary, setSummary] = useState<ConversionSummary | null>(null);
  const [hasConverted, setHasConverted] = useState(false);
  const [diagOpen, setDiagOpen] = useState(false);
  const [pdfUrl, setPdfUrl] = useState<string | null>(null);
  const [pdfDataUrl, setPdfDataUrl] = useState<string | null>(null);
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
    // The /frameworks endpoint lists the PDF code-migration providers; designer frameworks are static.
    if (!isDesigner) {
      fetch(`${API_BASE}/frameworks`)
        .then(r => r.json())
        .then((data: Framework[]) => setFrameworks(data))
        .catch(() => { /* use fallback */ });
    }
    return () => { if (prevPdfUrl.current) URL.revokeObjectURL(prevPdfUrl.current); };
  }, [isDesigner]);

  const current = frameworks.find(f => f.id === selectedId);

  const handleFrameworkChange = (id: string) => {
    setSelectedId(id);
    setSourceCode('');
    setResourceJson('');
    setResourceXml('');
    setResourceFileName('');
    setJrxmlResourceMap({});
    setJrxmlResourceFileNames([]);
    setRpxResourceMap({});
    setRpxResourceFileNames([]);
    setPxaCode('');
    setDiagnostics([]);
    setSummary(null);
    setHasConverted(false);
    setDiagOpen(false);
    setPdfUrl(null);
    setError(null);
    setReportDesign(null);
  };

  // Keep the selected framework valid for the active Code sub-tab (PDF vs Spreadsheet).
  useEffect(() => {
    if (isDesigner) return;
    const visible = frameworks.filter(f => (kindFilter === 'spreadsheet' ? f.kind === 'spreadsheet' : f.kind !== 'spreadsheet'));
    if (visible.length > 0 && !visible.some(f => f.id === selectedId)) handleFrameworkChange(visible[0].id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [codeKind, frameworks]);

  const applyConvertResult = (data: { pxaCode?: string; diagnostics?: Diagnostic[]; summary?: ConversionSummary }) => {
    const diags = data.diagnostics ?? [];
    setPxaCode(data.pxaCode ?? '');
    setDiagnostics(diags);
    setSummary(data.summary ?? null);
    setHasConverted(true);
    setDiagOpen(diags.some(d => d.severity === 'Warning'));
  };

  const handleResourceFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file) return;

    try {
      setResourceXml(await file.text());
      setResourceFileName(file.name);
      setError(null);
    } catch {
      setError(`Could not read ${file.name}.`);
    }
  };

  const handleJrxmlResourceFilesChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(e.target.files ?? []);
    e.target.value = '';
    const jrxmlFiles = files.filter(file => file.name.toLowerCase().endsWith('.jrxml'));
    if (jrxmlFiles.length === 0) return;

    try {
      const entries = await Promise.all(
        jrxmlFiles.map(async file => [file.name, await file.text()] as const)
      );
      setJrxmlResourceMap(currentResources => ({
        ...currentResources,
        ...Object.fromEntries(entries),
      }));
      setJrxmlResourceFileNames(currentNames => {
        const names = new Set(currentNames);
        for (const file of jrxmlFiles) names.add(file.name);
        return Array.from(names).sort((a, b) => a.localeCompare(b));
      });
      setError(null);
    } catch {
      setError('Could not read one or more .jrxml resource files.');
    }
  };

  const handleRpxResourceFilesChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(e.target.files ?? []);
    e.target.value = '';
    const rpxFiles = files.filter(file => file.name.toLowerCase().endsWith('.rpx'));
    if (rpxFiles.length === 0) return;

    try {
      const entries = await Promise.all(
        rpxFiles.map(async file => [file.name, await file.text()] as const)
      );
      setRpxResourceMap(currentResources => ({
        ...currentResources,
        ...Object.fromEntries(entries),
      }));
      setRpxResourceFileNames(currentNames => {
        const names = new Set(currentNames);
        for (const file of rpxFiles) names.add(file.name);
        return Array.from(names).sort((a, b) => a.localeCompare(b));
      });
      setError(null);
    } catch {
      setError('Could not read one or more .rpx resource files.');
    }
  };

  // Load a binary/zip report package (.trdp, packaged .rdlx) and send it base64-encoded as sourceBase64;
  // the backend unzips it and converts the inner report.
  const handlePackageFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    try {
      const bytes = new Uint8Array(await file.arrayBuffer());
      let binary = '';
      const chunk = 0x8000;
      for (let i = 0; i < bytes.length; i += chunk)
        binary += String.fromCharCode(...bytes.subarray(i, i + chunk));
      setSourceBase64(btoa(binary));
      setBinaryFileName(file.name);
      setSourceCode(`/* Binary report package loaded: ${file.name} (${bytes.length} bytes). Click Convert. */`);
      setError(null);
    } catch {
      setError('Could not read the report package file.');
    } finally {
      e.target.value = '';
    }
  };

  const handleConvert = async () => {
    if (!sourceCode.trim() && !sourceBase64) return;
    setConverting(true);
    setError(null);
    try {
      // Report (DevExpress XtraReport or RDL/RDLC) → PXA design (JSON), opened in the visual designer.
      if (isReportDesign(selectedId)) {
        let resources: Record<string, string> = {};
        if (selectedId === JRXML_REPORT_ID) resources = { ...resources, ...jrxmlResourceMap };
        if (selectedId === RPX_REPORT_ID) resources = { ...resources, ...rpxResourceMap };
        if (resourceJson.trim()) {
          const parsed = JSON.parse(resourceJson);
          if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
            throw new Error('Resource JSON must be an object like { "logo.ImageSource": "..." }.');
          }
          resources = {
            ...resources,
            ...Object.fromEntries(
              Object.entries(parsed).map(([key, value]) => [key, String(value ?? '')])
            ),
          };
        }
        const res = await fetch(`${API_BASE}/report-to-design`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            sourceCode,
            sourceBase64: sourceBase64 ?? undefined,
            resources: Object.keys(resources).length > 0 ? resources : undefined,
            resourceXml: resourceXml.trim() ? resourceXml : undefined,
          }),
        });
        if (!res.ok) { const e = await res.json(); throw new Error(e.error ?? `HTTP ${res.status}`); }
        const data = await res.json();
        const diags: Diagnostic[] = data.diagnostics ?? [];
        const elementCount = data.design?.pages?.[0]?.elements?.length ?? 0;
        setReportDesign(data.design);
        setPxaCode(JSON.stringify(data.design, null, 2));
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
      setError(e.message ?? 'Conversion failed — is the Power Dox Automation backend running on port 5086?');
    } finally {
      setConverting(false);
    }
  };

  const handleOpenInDesigner = () => {
    if (!reportDesign) return;
    const pages = (reportDesign.pages ?? []).map((p: any) => ({ id: p.id, elements: p.elements ?? [] }));
    const importedPageSettings = reportDesign.pageSettings ?? {};
    const template: Template = {
      id: reportDesign.id ?? `report-design-${Date.now()}`,
      name: reportDesign.name ?? 'Imported report',
      category: reportDesign.category ?? 'imported',
      description: reportDesign.description ?? 'Imported from a report designer.',
      pages: pages.length ? pages : [{ id: 'page-1', elements: [] }],
      sharedElements: reportDesign.sharedElements ?? [],
      data: reportDesign.data ?? {},
    };
    const pageSettings = normalizePageSettings({
      ...importedPageSettings,
      width: importedPageSettings.width ?? DEFAULT_PAGE_SETTINGS.width,
      height: importedPageSettings.height ?? DEFAULT_PAGE_SETTINGS.height,
      orientation: importedPageSettings.orientation
        ?? ((importedPageSettings.width ?? 0) > (importedPageSettings.height ?? 0) ? 'landscape' : 'portrait'),
      unit: importedPageSettings.unit ?? 'pt',
      showMarginGuide: false,
    });

    sessionStorage.setItem('pxa_migration_designer_handoff', JSON.stringify({
      template,
      pageSettings,
    }));
    setCurrentTemplate(template);
    updatePageSettings(pageSettings);
    localStorage.setItem('pxa_last_template', template.name);
    navigate('/create?source=migration');
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
      setPdfDataUrl(await blobToDataUrl(blob));
    } catch (e: any) {
      setError(e.message ?? 'Preview failed — is the Power Dox Automation backend running on port 5086?');
    } finally {
      setPreviewing(false);
    }
  };

  const handleCopy = async () => {
    if (!pxaCode) return;
    await navigator.clipboard.writeText(pxaCode);
    setCopyLabel('Copied!');
    setTimeout(() => setCopyLabel('Copy'), 2000);
  };

  const handleDownload = () => {
    if (!pxaCode) return;
    const blob = new Blob([pxaCode], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'migration.cs';
    a.click();
    URL.revokeObjectURL(url);
  };

  const handleOpenPreviewInViewer = () => {
    if (!pdfDataUrl) return;
    writePdfViewerHandoff({
      dataUrl: pdfDataUrl,
      name: `${selectedId}-migration-preview.pdf`,
    });
    navigate('/pdf-viewer?handoff=session');
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

  const isSpreadsheetCode = !isDesigner && kindFilter === 'spreadsheet';
  return (
    <div className="mgr-page">
      <AppHeader activePage="migrations" />
      {isSpreadsheetCode
        ? <MigrationTabs tabs={sheetTabs('code')} />
        : <MigrationTabs tabs={pdfTabs(isDesigner ? 'designer' : 'code')} />}

      <main className="mgr-main">
        {/* Page heading */}
        <div className="mgr-heading">
          <div className="mgr-heading-left">
            {isDesigner ? <FiLayout className="mgr-heading-icon" /> : <FiCode className="mgr-heading-icon" />}
            <div>
              <h1>{isDesigner ? 'UI-Designer Migration' : isSpreadsheetCode ? 'Spreadsheet Code Migration' : 'PDF Code Migration'}</h1>
              <p>
                {isDesigner
                  ? 'Convert a report-designer file (DevExpress, RDL/RDLC, ActiveReports, FastReport, Telerik) into an editable PXA design, then open it in the visual designer.'
                  : isSpreadsheetCode
                    ? 'Paste code from another spreadsheet library (ClosedXML, EPPlus, GemBox, Aspose.Cells), convert it to the PXA spreadsheet API, and preview the result as a grid.'
                    : 'Paste code from another PDF library, convert it to PXA-compatible PDF code, and preview the result instantly.'}
              </p>
            </div>
          </div>
          <button className="mgr-btn" onClick={() => navigate('/migrations')} title="Back to Migrations">
            ← Migrations
          </button>
        </div>

        {/* Framework selector */}
        <div className="mgr-framework-bar">
          <label htmlFor="mgr-fw-select">Source framework</label>
          <select
            id="mgr-fw-select"
            value={selectedId}
            onChange={e => handleFrameworkChange(e.target.value)}
          >
            {(() => {
              const label = (f: Framework) =>
                `${f.name}${f.status === 'skeleton' ? ' (skeleton)' : f.status === 'pilot' ? ' (pilot)' : ''}`;
              const opt = (f: Framework) => <option key={f.id} value={f.id}>{label(f)}</option>;
              // Code mode: the PDF | Spreadsheet sub-tab filters by kind. Designer mode: show all.
              const visible = isDesigner
                ? frameworks
                : frameworks.filter(f => (kindFilter === 'spreadsheet' ? f.kind === 'spreadsheet' : f.kind !== 'spreadsheet'));
              return visible.map(opt);
            })()}
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
                onChange={v => { setSourceCode(v ?? ''); if (sourceBase64) { setSourceBase64(null); setBinaryFileName(''); } }}
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
            {isReportDesign(selectedId) && (
              <div className="mgr-resource-panel">
                <div className="mgr-resource-header">
                  <label htmlFor="mgr-resource-file">Resources</label>
                  <span>{resourceFileName || 'No .resx loaded'}</span>
                </div>
                <div className="mgr-resource-actions">
                  <label className="mgr-file-btn" htmlFor="mgr-resource-file">
                    <FiUpload /> Load .resx
                  </label>
                  <input
                    id="mgr-resource-file"
                    type="file"
                    accept=".resx,.xml"
                    onChange={handleResourceFileChange}
                  />
                  {(resourceXml || resourceFileName) && (
                    <button
                      type="button"
                      className="mgr-link-btn"
                      onClick={() => {
                        setResourceXml('');
                        setResourceFileName('');
                      }}
                    >
                      Clear
                    </button>
                  )}
                </div>
                <div className="mgr-resource-header">
                  <label htmlFor="mgr-package-file">Report package (zip)</label>
                  <span>{binaryFileName || 'No .trdp / packaged .rdlx loaded'}</span>
                </div>
                <div className="mgr-resource-actions">
                  <label className="mgr-file-btn" htmlFor="mgr-package-file">
                    <FiUpload /> Load .trdp / .rdlx
                  </label>
                  <input
                    id="mgr-package-file"
                    type="file"
                    accept=".trdp,.trdx,.rdlx,.zip"
                    onChange={handlePackageFileChange}
                  />
                  {sourceBase64 && (
                    <button
                      type="button"
                      className="mgr-link-btn"
                      onClick={() => { setSourceBase64(null); setBinaryFileName(''); setSourceCode(''); }}
                    >
                      Clear
                    </button>
                  )}
                </div>
                {selectedId === JRXML_REPORT_ID && (
                  <>
                    <div className="mgr-resource-header">
                      <label htmlFor="mgr-jrxml-resource-files">JRXML subreports</label>
                      <span>{jrxmlResourceFileNames.length ? `${jrxmlResourceFileNames.length} loaded` : 'No .jrxml resources loaded'}</span>
                    </div>
                    <div className="mgr-resource-actions">
                      <label className="mgr-file-btn" htmlFor="mgr-jrxml-resource-files">
                        <FiUpload /> Load .jrxml files
                      </label>
                      <input
                        id="mgr-jrxml-resource-files"
                        type="file"
                        accept=".jrxml"
                        multiple
                        onChange={handleJrxmlResourceFilesChange}
                      />
                      {jrxmlResourceFileNames.length > 0 && (
                        <button
                          type="button"
                          className="mgr-link-btn"
                          onClick={() => {
                            setJrxmlResourceMap({});
                            setJrxmlResourceFileNames([]);
                          }}
                        >
                          Clear
                        </button>
                      )}
                    </div>
                    {jrxmlResourceFileNames.length > 0 && (
                      <div className="mgr-resource-list">
                        {jrxmlResourceFileNames.map(name => <span key={name}>{name}</span>)}
                      </div>
                    )}
                  </>
                )}
                {selectedId === RPX_REPORT_ID && (
                  <>
                    <div className="mgr-resource-header">
                      <label htmlFor="mgr-rpx-resource-files">RPX subreports</label>
                      <span>{rpxResourceFileNames.length ? `${rpxResourceFileNames.length} loaded` : 'No .rpx resources loaded'}</span>
                    </div>
                    <div className="mgr-resource-actions">
                      <label className="mgr-file-btn" htmlFor="mgr-rpx-resource-files">
                        <FiUpload /> Load .rpx files
                      </label>
                      <input
                        id="mgr-rpx-resource-files"
                        type="file"
                        accept=".rpx"
                        multiple
                        onChange={handleRpxResourceFilesChange}
                      />
                      {rpxResourceFileNames.length > 0 && (
                        <button
                          type="button"
                          className="mgr-link-btn"
                          onClick={() => {
                            setRpxResourceMap({});
                            setRpxResourceFileNames([]);
                          }}
                        >
                          Clear
                        </button>
                      )}
                    </div>
                    {rpxResourceFileNames.length > 0 && (
                      <div className="mgr-resource-list">
                        {rpxResourceFileNames.map(name => <span key={name}>{name}</span>)}
                      </div>
                    )}
                  </>
                )}
                <label htmlFor="mgr-resource-json">Resource JSON overrides</label>
                <textarea
                  id="mgr-resource-json"
                  value={resourceJson}
                  onChange={e => setResourceJson(e.target.value)}
                  spellCheck={false}
                  placeholder='{ "logo.ImageSource": "iVBORw0KGgo..." }'
                />
              </div>
            )}
            <div className="mgr-pane-footer">
              <button
                className="mgr-btn mgr-btn-primary"
                onClick={handleConvert}
                disabled={converting || (!sourceCode.trim() && !sourceBase64)}
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
              <span>{isReportDesign(selectedId) ? 'PXA Design (JSON)' : current?.kind === 'spreadsheet' ? 'PXA Spreadsheet Code' : 'PXA-compatible PDF Code'}</span>
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
                <button className="mgr-icon-btn" onClick={handleCopy} disabled={!pxaCode} title="Copy to clipboard">
                  <FiCopy /> {copyLabel}
                </button>
                <button className="mgr-icon-btn" onClick={handleDownload} disabled={!pxaCode} title="Download as .cs file">
                  <FiDownload /> Download .cs
                </button>
              </div>
            </div>
            <div className="mgr-editor-wrapper">
              {diffMode && pxaCode ? (
                <DiffEditor
                  language="csharp"
                  original={sourceCode}
                  modified={pxaCode}
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
                  value={pxaCode}
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

        {/* Preview — PDF for PDF code, an HTML grid for spreadsheet code */}
        <div className="mgr-preview">
          <div className="mgr-preview-header">
            <span>{isSpreadsheetCode ? 'Spreadsheet Preview' : 'PDF Preview'}</span>
            {pdfDataUrl && !isSpreadsheetCode && (
              <button className="mgr-preview-action" type="button" onClick={handleOpenPreviewInViewer}>
                <FiExternalLink />
                Open in PDF Viewer
              </button>
            )}
          </div>
          {pdfUrl
            ? <iframe className="mgr-pdf-frame" src={pdfUrl} title={isSpreadsheetCode ? 'Spreadsheet Preview' : 'PDF Preview'} />
            : (
              <div className="mgr-pdf-empty">
                <FiPlay size={32} />
                <p>Click <strong>Generate Preview</strong> to render the converted {isSpreadsheetCode ? 'workbook as a grid' : 'PDF'}</p>
              </div>
            )
          }
        </div>
      </main>
    </div>
  );
};

export default MigrationsPage;
