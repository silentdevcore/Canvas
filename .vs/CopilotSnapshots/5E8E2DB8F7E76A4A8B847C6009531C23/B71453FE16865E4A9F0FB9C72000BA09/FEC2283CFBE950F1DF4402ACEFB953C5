using Canvas.MinimalPdf;

var doc = new PdfDocument();
var page = doc.AddPage();

page.DrawText("Hello World", x: 100, y: 700);
page.DrawText("Minimal extensible PDF engine", x: 100, y: 675, fontSize: 12);

doc.Save("output.pdf");

Console.WriteLine("PDF generated: output.pdf");
