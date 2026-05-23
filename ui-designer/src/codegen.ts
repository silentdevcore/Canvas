export function generateCSharp(elements: Record<string, any>, rootIds: string[]): string {
  function renderElement(id: string, indent = 2): string {
    const el = elements[id];
    if (!el) return '';
    const pad = ' '.repeat(indent);
    if (el.type === 'Text') {
      return `${pad}new TextBlock { Text = \"${el.props.text}\", FontSize = ${el.props.fontSize} }`;
    }
    if (el.type === 'Column') {
      const children = (el.children || []).map((cid: string) => renderElement(cid, indent + 2)).join(',\n');
      return `${pad}new StackPanel { Orientation = Orientation.Vertical, Children = {\n${children}\n${pad}} }`;
    }
    if (el.type === 'Table') {
      const rows = el.props.rows;
      const columns = el.props.columns;
      const data = el.props.data || [];
      let cells = [];
      for (let r = 0; r < rows; r++) {
        for (let c = 0; c < columns; c++) {
          const value = data[r] && data[r][c] ? data[r][c] : '';
          cells.push(`${pad}    new TableCell { Content = "${value}" }`);
        }
      }
      return `${pad}new Table { Rows = ${rows}, Columns = ${columns}, Cells = {\n${cells.join(',\n')}\n${pad}} }`;
    }
    if (el.type === 'Image') {
      return `${pad}new ImageElement { Source = "${el.props.src}", Width = ${el.props.width}, Height = ${el.props.height}, Alt = "${el.props.alt}" }`;
    }
    if (el.type === 'Rectangle') {
      return `${pad}new RectangleElement { Width = ${el.props.width}, Height = ${el.props.height}, FillColor = "${el.props.fillColor}", StrokeColor = "${el.props.strokeColor}", StrokeWidth = ${el.props.strokeWidth}, BorderRadius = ${el.props.borderRadius} }`;
    }
    if (el.type === 'Circle') {
      return `${pad}new CircleElement { Radius = ${el.props.radius}, FillColor = "${el.props.fillColor}", StrokeColor = "${el.props.strokeColor}", StrokeWidth = ${el.props.strokeWidth} }`;
    }
    if (el.type === 'Line') {
      return `${pad}new LineElement { X1 = ${el.props.x1}, Y1 = ${el.props.y1}, X2 = ${el.props.x2}, Y2 = ${el.props.y2}, StrokeColor = "${el.props.strokeColor}", StrokeWidth = ${el.props.strokeWidth}, LineCap = LineCap.${el.props.lineCap} }`;
    }
    if (el.type === 'Link') {
      return `${pad}new LinkAnnotation { Url = "${el.props.url}", Text = "${el.props.text}", Width = ${el.props.width}, Height = ${el.props.height} }`;
    }
    if (el.type === 'List') {
      const items = (el.props.items || []).map((item: string) => `"${item}"`).join(', ');
      return `${pad}new ListElement { Items = new[] { ${items} }, Ordered = ${el.props.ordered}, MarkerStyle = "${el.props.markerStyle}" }`;
    }
    if (el.type === 'PageBreak') {
      return `${pad}// Page break - forces content to next page\n${pad}document.AddPageBreak();`;
    }
    if (el.type === 'Grid') {
      const children = (el.children || []).map((cid: string) => renderElement(cid, indent + 2)).join(',\n');
      return `${pad}new GridElement {\n${pad}  Rows = ${el.props.rows || 2},\n${pad}  Columns = ${el.props.columns || 3},\n${pad}  Gap = ${el.props.gap || 10},\n${pad}  JustifyContent = "${el.props.justifyContent || 'start'}",\n${pad}  AlignItems = "${el.props.alignItems || 'start'}",\n${pad}  Children = {\n${children}\n${pad}  }\n${pad}}`;
    }
    if (el.type === 'Spacer') {
      return `${pad}new SpacerElement { Width = ${el.props.width || 100}, Height = ${el.props.height || 20}, FlexGrow = ${el.props.flexGrow || 0} }`;
    }
    if (el.type === 'Button') {
      return `${pad}// Button element - interactive elements may not be applicable for PDF generation\n${pad}// new ButtonElement { Text = "${el.props.text || 'Button'}", Style = "${el.props.style || 'primary'}", Action = "${el.props.action || 'click'}" }`;
    }
    if (el.type === 'Checkbox') {
      return `${pad}// Checkbox element - form elements may not be applicable for PDF generation\n${pad}// new CheckboxElement { Label = "${el.props.label || 'Checkbox'}", IsChecked = ${el.props.checked || false} }`;
    }
    if (el.type === 'Radio') {
      return `${pad}// Radio element - form elements may not be applicable for PDF generation\n${pad}// new RadioElement { Label = "${el.props.label || 'Radio Button'}", IsChecked = ${el.props.checked || false}, GroupName = "${el.props.groupName || 'radio-group'}" }`;
    }
    return '';
  }
  return `// Generated C# (WPF style)\nvar layout = new StackPanel {\n  Orientation = Orientation.Vertical,\n  Children = {\n${rootIds.map((id) => renderElement(id, 4)).join(',\n')}\n  }\n};`;
}
