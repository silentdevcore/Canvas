export type PxaChartType =
  | 'bar'
  | 'line'
  | 'area'
  | 'pie'
  | 'doughnut'
  | 'stackedBar'
  | 'combo';

export interface ChartSeries {
  id: string;
  name: string;
  type?: PxaChartType;
  values: Array<number | null>;
  color?: string;
  stackGroup?: string;
  valueAxisId?: string;
  fill?: boolean;
  showMarkers?: boolean;
}

export interface ChartAxis {
  id: string;
  title?: string;
  minimum?: number;
  maximum?: number;
  scale?: 'linear' | 'logarithmic';
  numberFormat?: string;
  visible?: boolean;
  gridLines?: boolean;
}

export interface ChartDefinition {
  schemaVersion: 2;
  type: PxaChartType;
  title?: string;
  subtitle?: string;
  locale?: string;
  categories: string[];
  series: ChartSeries[];
  categoryAxis?: ChartAxis;
  valueAxes?: ChartAxis[];
  legend?: { visible: boolean; position: 'top' | 'right' | 'bottom' | 'left' };
  dataLabels?: { visible: boolean; position?: string; numberFormat?: string };
  palette?: string[];
  binding?: {
    dataPath?: string;
    categoryField?: string;
    seriesField?: string;
    valueField?: string;
    aggregation?: 'sum' | 'average' | 'minimum' | 'maximum' | 'count';
    sort?: 'source' | 'ascending' | 'descending';
  };
  recognition?: {
    status: 'native' | 'automatic' | 'reviewRequired' | 'visualFallback';
    confidence: number;
    sourceKind?: 'pxaMetadata' | 'pdfVector' | 'pdfRaster';
    sourceAssetId?: string;
    diagnosticCode?: string;
  };
}
