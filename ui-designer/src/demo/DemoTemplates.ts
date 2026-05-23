/**
 * Demo templates showcasing the full capabilities of the UI Designer
 */

export interface DemoTemplate {
  id: string;
  name: string;
  description: string;
  category: string;
  template: any;
  sampleData: any;
}

export const demoTemplates: DemoTemplate[] = [
  {
    id: 'invoice-template',
    name: 'Professional Invoice',
    description: 'A complete invoice template with dynamic line items, calculations, and professional formatting',
    category: 'Business',
    sampleData: {
      invoiceNumber: 'INV-2024-001',
      date: '2024-01-15',
      dueDate: '2024-02-15',
      company: {
        name: 'Acme Corporation',
        address: '123 Business St, Suite 100\nNew York, NY 10001',
        phone: '(555) 123-4567',
        email: 'billing@acme.com'
      },
      customer: {
        name: 'John Smith',
        address: '456 Customer Ave\nLos Angeles, CA 90210',
        email: 'john.smith@email.com'
      },
      items: [
        { description: 'Web Development Services', quantity: 40, rate: 125.00, amount: 5000.00 },
        { description: 'UI/UX Design Consultation', quantity: 20, rate: 150.00, amount: 3000.00 },
        { description: 'Project Management', quantity: 10, rate: 100.00, amount: 1000.00 }
      ],
      subtotal: 9000.00,
      taxRate: 0.08,
      tax: 720.00,
      total: 9720.00,
      notes: 'Payment due within 30 days. Late payments subject to 1.5% monthly interest.'
    },
    template: {
      id: 'invoice-template',
      name: 'Professional Invoice',
      pageSettings: {
        width: 8.5,
        height: 11,
        unit: 'inches',
        margins: { top: 0.5, right: 0.5, bottom: 0.5, left: 0.5 }
      },
      elements: [
        // Header
        {
          id: 'header-bg',
          type: 'rectangle',
          x: 0,
          y: 0,
          width: 8.5,
          height: 1.2,
          style: { fill: '#2563eb', stroke: 'none' }
        },
        {
          id: 'company-name',
          type: 'text',
          x: 0.5,
          y: 0.3,
          width: 4,
          height: 0.3,
          properties: {
            text: { value: 'company.name' },
            fontSize: 18,
            fontWeight: 'bold',
            color: '#ffffff'
          }
        },
        {
          id: 'invoice-title',
          type: 'text',
          x: 5.5,
          y: 0.3,
          width: 2.5,
          height: 0.3,
          properties: {
            text: { value: '"INVOICE"' },
            fontSize: 24,
            fontWeight: 'bold',
            color: '#ffffff',
            align: 'right'
          }
        },

        // Invoice details
        {
          id: 'invoice-number',
          type: 'text',
          x: 5.5,
          y: 0.8,
          width: 2.5,
          height: 0.2,
          properties: {
            text: { value: 'invoiceNumber' },
            fontSize: 12,
            color: '#ffffff',
            align: 'right'
          }
        },

        // Company and customer info
        {
          id: 'company-info',
          type: 'text',
          x: 0.5,
          y: 1.5,
          width: 3.5,
          height: 1,
          properties: {
            text: { value: 'company.address' },
            fontSize: 10,
            lineHeight: 1.4
          }
        },
        {
          id: 'customer-info',
          type: 'text',
          x: 4.5,
          y: 1.5,
          width: 3.5,
          height: 1,
          properties: {
            text: { value: 'customer.address' },
            fontSize: 10,
            lineHeight: 1.4
          }
        },

        // Invoice table header
        {
          id: 'table-header-bg',
          type: 'rectangle',
          x: 0.5,
          y: 2.8,
          width: 7.5,
          height: 0.3,
          style: { fill: '#f3f4f6', stroke: '#d1d5db' }
        },
        {
          id: 'header-description',
          type: 'text',
          x: 0.6,
          y: 2.9,
          width: 3,
          height: 0.2,
          properties: {
            text: { value: '"Description"' },
            fontSize: 10,
            fontWeight: 'bold'
          }
        },
        {
          id: 'header-qty',
          type: 'text',
          x: 3.7,
          y: 2.9,
          width: 0.8,
          height: 0.2,
          properties: {
            text: { value: '"Qty"' },
            fontSize: 10,
            fontWeight: 'bold',
            align: 'center'
          }
        },
        {
          id: 'header-rate',
          type: 'text',
          x: 4.6,
          y: 2.9,
          width: 1.2,
          height: 0.2,
          properties: {
            text: { value: '"Rate"' },
            fontSize: 10,
            fontWeight: 'bold',
            align: 'right'
          }
        },
        {
          id: 'header-amount',
          type: 'text',
          x: 6,
          y: 2.9,
          width: 1.8,
          height: 0.2,
          properties: {
            text: { value: '"Amount"' },
            fontSize: 10,
            fontWeight: 'bold',
            align: 'right'
          }
        },

        // Invoice items (repeat)
        {
          id: 'invoice-items',
          type: 'container',
          x: 0.5,
          y: 3.2,
          width: 7.5,
          height: 2,
          properties: {
            repeatSource: 'items',
            itemAlias: 'item'
          },
          children: [
            {
              id: 'item-description',
              type: 'text',
              x: 0.1,
              y: 0.1,
              width: 3,
              height: 0.3,
              properties: {
                text: { value: 'item.description' },
                fontSize: 10
              }
            },
            {
              id: 'item-qty',
              type: 'text',
              x: 3.2,
              y: 0.1,
              width: 0.8,
              height: 0.3,
              properties: {
                text: { value: 'item.quantity' },
                fontSize: 10,
                align: 'center'
              }
            },
            {
              id: 'item-rate',
              type: 'text',
              x: 4.1,
              y: 0.1,
              width: 1.2,
              height: 0.3,
              properties: {
                text: { value: 'item.rate', formatter: 'currency' },
                fontSize: 10,
                align: 'right'
              }
            },
            {
              id: 'item-amount',
              type: 'text',
              x: 5.5,
              y: 0.1,
              width: 1.8,
              height: 0.3,
              properties: {
                text: { value: 'item.amount', formatter: 'currency' },
                fontSize: 10,
                align: 'right'
              }
            }
          ]
        },

        // Totals
        {
          id: 'subtotal-label',
          type: 'text',
          x: 5,
          y: 5.5,
          width: 1.5,
          height: 0.2,
          properties: {
            text: { value: '"Subtotal:"' },
            fontSize: 10,
            align: 'right'
          }
        },
        {
          id: 'subtotal-value',
          type: 'text',
          x: 6.5,
          y: 5.5,
          width: 1.5,
          height: 0.2,
          properties: {
            text: { value: 'subtotal', formatter: 'currency' },
            fontSize: 10,
            align: 'right'
          }
        },
        {
          id: 'tax-label',
          type: 'text',
          x: 5,
          y: 5.8,
          width: 1.5,
          height: 0.2,
          properties: {
            text: { value: '"Tax:"' },
            fontSize: 10,
            align: 'right'
          }
        },
        {
          id: 'tax-value',
          type: 'text',
          x: 6.5,
          y: 5.8,
          width: 1.5,
          height: 0.2,
          properties: {
            text: { value: 'tax', formatter: 'currency' },
            fontSize: 10,
            align: 'right'
          }
        },
        {
          id: 'total-bg',
          type: 'rectangle',
          x: 4.5,
          y: 6.1,
          width: 3.5,
          height: 0.3,
          style: { fill: '#f3f4f6', stroke: '#d1d5db' }
        },
        {
          id: 'total-label',
          type: 'text',
          x: 5,
          y: 6.2,
          width: 1.5,
          height: 0.2,
          properties: {
            text: { value: '"TOTAL:"' },
            fontSize: 12,
            fontWeight: 'bold',
            align: 'right'
          }
        },
        {
          id: 'total-value',
          type: 'text',
          x: 6.5,
          y: 6.2,
          width: 1.5,
          height: 0.2,
          properties: {
            text: { value: 'total', formatter: 'currency' },
            fontSize: 12,
            fontWeight: 'bold',
            align: 'right'
          }
        },

        // Footer
        {
          id: 'notes',
          type: 'text',
          x: 0.5,
          y: 9.5,
          width: 7.5,
          height: 0.8,
          properties: {
            text: { value: 'notes' },
            fontSize: 9,
            color: '#6b7280',
            lineHeight: 1.3
          }
        }
      ]
    }
  },

  {
    id: 'certificate-template',
    name: 'Achievement Certificate',
    description: 'An elegant certificate template with conditional content and professional styling',
    category: 'Education',
    sampleData: {
      certificateTitle: 'Certificate of Achievement',
      recipientName: 'Sarah Johnson',
      achievement: 'Outstanding Performance in Web Development',
      date: '2024-01-20',
      issuer: {
        name: 'Tech Academy',
        title: 'Director of Education',
        signatureName: 'Dr. Michael Chen'
      },
      honors: ['Summa Cum Laude', 'Deans List', 'Perfect Attendance'],
      score: 98,
      grade: 'A+',
      showHonors: true
    },
    template: {
      id: 'certificate-template',
      name: 'Achievement Certificate',
      pageSettings: {
        width: 11,
        height: 8.5,
        unit: 'inches',
        margins: { top: 1, right: 1, bottom: 1, left: 1 }
      },
      elements: [
        // Border
        {
          id: 'border',
          type: 'rectangle',
          x: 0.5,
          y: 0.5,
          width: 10,
          height: 7.5,
          style: { fill: 'none', stroke: '#d4af37', strokeWidth: 3 }
        },

        // Header
        {
          id: 'header-bg',
          type: 'rectangle',
          x: 0.5,
          y: 0.5,
          width: 10,
          height: 1.5,
          style: { fill: '#1f2937', stroke: 'none' }
        },
        {
          id: 'certificate-title',
          type: 'text',
          x: 1,
          y: 0.8,
          width: 9,
          height: 0.5,
          properties: {
            text: { value: 'certificateTitle' },
            fontSize: 36,
            fontWeight: 'bold',
            color: '#d4af37',
            align: 'center'
          }
        },
        {
          id: 'issuer-name',
          type: 'text',
          x: 1,
          y: 1.4,
          width: 9,
          height: 0.3,
          properties: {
            text: { value: 'issuer.name' },
            fontSize: 14,
            color: '#ffffff',
            align: 'center'
          }
        },

        // Main content
        {
          id: 'presented-to',
          type: 'text',
          x: 2,
          y: 2.5,
          width: 7,
          height: 0.3,
          properties: {
            text: { value: '"This is to certify that"' },
            fontSize: 16,
            align: 'center'
          }
        },
        {
          id: 'recipient-name',
          type: 'text',
          x: 2,
          y: 3,
          width: 7,
          height: 0.5,
          properties: {
            text: { value: 'recipientName' },
            fontSize: 28,
            fontWeight: 'bold',
            color: '#1f2937',
            align: 'center'
          }
        },
        {
          id: 'has-achieved',
          type: 'text',
          x: 2,
          y: 3.7,
          width: 7,
          height: 0.3,
          properties: {
            text: { value: '"has achieved"' },
            fontSize: 16,
            align: 'center'
          }
        },
        {
          id: 'achievement',
          type: 'text',
          x: 1.5,
          y: 4.2,
          width: 8,
          height: 0.8,
          properties: {
            text: { value: 'achievement' },
            fontSize: 18,
            fontWeight: 'bold',
            color: '#2563eb',
            align: 'center',
            lineHeight: 1.4
          }
        },

        // Honors section (conditional)
        {
          id: 'honors-section',
          type: 'container',
          x: 2,
          y: 5.2,
          width: 7,
          height: 1,
          properties: {
            visibleWhen: 'showHonors'
          },
          children: [
            {
              id: 'honors-title',
              type: 'text',
              x: 0,
              y: 0,
              width: 7,
              height: 0.3,
              properties: {
                text: { value: '"With Honors:"' },
                fontSize: 14,
                fontWeight: 'bold',
                align: 'center'
              }
            },
            {
              id: 'honors-list',
              type: 'container',
              x: 0,
              y: 0.4,
              width: 7,
              height: 0.6,
              properties: {
                repeatSource: 'honors',
                itemAlias: 'honor'
              },
              children: [
                {
                  id: 'honor-item',
                  type: 'text',
                  x: 0,
                  y: 0,
                  width: 7,
                  height: 0.2,
                  properties: {
                    text: { value: 'honor' },
                    fontSize: 12,
                    align: 'center'
                  }
                }
              ]
            }
          ]
        },

        // Score and grade
        {
          id: 'score-grade',
          type: 'text',
          x: 2,
          y: 6.5,
          width: 7,
          height: 0.3,
          properties: {
            text: { value: '`Score: ${score}% | Grade: ${grade}`' },
            fontSize: 14,
            align: 'center'
          }
        },

        // Date and signature
        {
          id: 'date',
          type: 'text',
          x: 2,
          y: 7,
          width: 3,
          height: 0.2,
          properties: {
            text: { value: 'date', formatter: 'date' },
            fontSize: 12,
            align: 'center'
          }
        },
        {
          id: 'signature-line',
          type: 'line',
          x: 6,
          y: 7.2,
          width: 2,
          height: 0,
          style: { stroke: '#000000', strokeWidth: 1 }
        },
        {
          id: 'signature-name',
          type: 'text',
          x: 6,
          y: 7.3,
          width: 2,
          height: 0.2,
          properties: {
            text: { value: 'issuer.signatureName' },
            fontSize: 12,
            align: 'center'
          }
        },
        {
          id: 'signature-title',
          type: 'text',
          x: 6,
          y: 7.5,
          width: 2,
          height: 0.2,
          properties: {
            text: { value: 'issuer.title' },
            fontSize: 10,
            align: 'center'
          }
        }
      ]
    }
  },

  {
    id: 'report-template',
    name: 'Business Report',
    description: 'A comprehensive business report with charts, tables, and dynamic content',
    category: 'Business',
    sampleData: {
      reportTitle: 'Q4 2024 Sales Report',
      companyName: 'Global Solutions Inc.',
      period: 'October - December 2024',
      executiveSummary: 'This quarter showed strong growth across all regions with a 25% increase in revenue compared to Q3.',
      metrics: {
        totalRevenue: 2500000,
        totalOrders: 1250,
        avgOrderValue: 2000,
        customerSatisfaction: 4.8
      },
      regions: [
        { name: 'North America', revenue: 1200000, orders: 600, growth: 15 },
        { name: 'Europe', revenue: 800000, orders: 400, growth: 22 },
        { name: 'Asia Pacific', revenue: 400000, orders: 200, growth: 35 },
        { name: 'Latin America', revenue: 100000, orders: 50, growth: 8 }
      ],
      topProducts: [
        { name: 'Enterprise Suite', sales: 450000, units: 225 },
        { name: 'Professional Services', sales: 380000, units: 190 },
        { name: 'Cloud Storage', sales: 320000, units: 160 },
        { name: 'Analytics Platform', sales: 280000, units: 140 }
      ],
      generatedDate: '2024-01-15',
      author: 'Jane Doe, CFO'
    },
    template: {
      id: 'report-template',
      name: 'Business Report',
      pageSettings: {
        width: 8.5,
        height: 11,
        unit: 'inches',
        margins: { top: 0.5, right: 0.5, bottom: 0.5, left: 0.5 }
      },
      elements: [
        // Header
        {
          id: 'header-bg',
          type: 'rectangle',
          x: 0,
          y: 0,
          width: 8.5,
          height: 1,
          style: { fill: '#1f2937', stroke: 'none' }
        },
        {
          id: 'report-title',
          type: 'text',
          x: 0.5,
          y: 0.2,
          width: 7.5,
          height: 0.4,
          properties: {
            text: { value: 'reportTitle' },
            fontSize: 24,
            fontWeight: 'bold',
            color: '#ffffff'
          }
        },
        {
          id: 'company-period',
          type: 'text',
          x: 0.5,
          y: 0.6,
          width: 7.5,
          height: 0.3,
          properties: {
            text: { value: '`companyName | ${period}`' },
            fontSize: 12,
            color: '#d1d5db'
          }
        },

        // Executive Summary
        {
          id: 'summary-title',
          type: 'text',
          x: 0.5,
          y: 1.2,
          width: 7.5,
          height: 0.3,
          properties: {
            text: { value: '"Executive Summary"' },
            fontSize: 18,
            fontWeight: 'bold',
            color: '#1f2937'
          }
        },
        {
          id: 'summary-text',
          type: 'text',
          x: 0.5,
          y: 1.6,
          width: 7.5,
          height: 0.6,
          properties: {
            text: { value: 'executiveSummary' },
            fontSize: 11,
            lineHeight: 1.4
          }
        },

        // Key Metrics
        {
          id: 'metrics-title',
          type: 'text',
          x: 0.5,
          y: 2.4,
          width: 7.5,
          height: 0.3,
          properties: {
            text: { value: '"Key Performance Metrics"' },
            fontSize: 16,
            fontWeight: 'bold'
          }
        },
        {
          id: 'metrics-grid',
          type: 'container',
          x: 0.5,
          y: 2.8,
          width: 7.5,
          height: 1.2,
          children: [
            {
              id: 'revenue-metric',
              type: 'text',
              x: 0,
              y: 0,
              width: 1.8,
              height: 0.5,
              properties: {
                text: { value: '`Total Revenue\n$${metrics.totalRevenue.toLocaleString()}`' },
                fontSize: 12,
                fontWeight: 'bold',
                align: 'center',
                lineHeight: 1.3
              }
            },
            {
              id: 'orders-metric',
              type: 'text',
              x: 2,
              y: 0,
              width: 1.8,
              height: 0.5,
              properties: {
                text: { value: '`Total Orders\n${metrics.totalOrders.toLocaleString()}`' },
                fontSize: 12,
                fontWeight: 'bold',
                align: 'center',
                lineHeight: 1.3
              }
            },
            {
              id: 'avg-order-metric',
              type: 'text',
              x: 4,
              y: 0,
              width: 1.8,
              height: 0.5,
              properties: {
                text: { value: '`Avg Order Value\n$${metrics.avgOrderValue.toLocaleString()}`' },
                fontSize: 12,
                fontWeight: 'bold',
                align: 'center',
                lineHeight: 1.3
              }
            },
            {
              id: 'satisfaction-metric',
              type: 'text',
              x: 6,
              y: 0,
              width: 1.5,
              height: 0.5,
              properties: {
                text: { value: '`Customer Satisfaction\n${metrics.customerSatisfaction}/5.0`' },
                fontSize: 12,
                fontWeight: 'bold',
                align: 'center',
                lineHeight: 1.3
              }
            }
          ]
        },

        // Regional Performance
        {
          id: 'regions-title',
          type: 'text',
          x: 0.5,
          y: 4.2,
          width: 7.5,
          height: 0.3,
          properties: {
            text: { value: '"Regional Performance"' },
            fontSize: 16,
            fontWeight: 'bold'
          }
        },
        {
          id: 'regions-table',
          type: 'container',
          x: 0.5,
          y: 4.6,
          width: 7.5,
          height: 2,
          properties: {
            repeatSource: 'regions',
            itemAlias: 'region'
          },
          children: [
            {
              id: 'region-name',
              type: 'text',
              x: 0,
              y: 0.1,
              width: 2,
              height: 0.3,
              properties: {
                text: { value: 'region.name' },
                fontSize: 11,
                fontWeight: 'bold'
              }
            },
            {
              id: 'region-revenue',
              type: 'text',
              x: 2.2,
              y: 0.1,
              width: 1.5,
              height: 0.3,
              properties: {
                text: { value: 'region.revenue', formatter: 'currency' },
                fontSize: 11,
                align: 'right'
              }
            },
            {
              id: 'region-orders',
              type: 'text',
              x: 3.8,
              y: 0.1,
              width: 1,
              height: 0.3,
              properties: {
                text: { value: 'region.orders' },
                fontSize: 11,
                align: 'center'
              }
            },
            {
              id: 'region-growth',
              type: 'text',
              x: 5,
              y: 0.1,
              width: 1,
              height: 0.3,
              properties: {
                text: { value: '`+${region.growth}%`' },
                fontSize: 11,
                color: '#059669',
                align: 'center'
              }
            }
          ]
        },

        // Top Products
        {
          id: 'products-title',
          type: 'text',
          x: 0.5,
          y: 7,
          width: 7.5,
          height: 0.3,
          properties: {
            text: { value: '"Top Performing Products"' },
            fontSize: 16,
            fontWeight: 'bold'
          }
        },
        {
          id: 'products-table',
          type: 'container',
          x: 0.5,
          y: 7.4,
          width: 7.5,
          height: 1.6,
          properties: {
            repeatSource: 'topProducts',
            itemAlias: 'product'
          },
          children: [
            {
              id: 'product-name',
              type: 'text',
              x: 0,
              y: 0.1,
              width: 3,
              height: 0.3,
              properties: {
                text: { value: 'product.name' },
                fontSize: 11
              }
            },
            {
              id: 'product-sales',
              type: 'text',
              x: 3.2,
              y: 0.1,
              width: 1.5,
              height: 0.3,
              properties: {
                text: { value: 'product.sales', formatter: 'currency' },
                fontSize: 11,
                align: 'right'
              }
            },
            {
              id: 'product-units',
              type: 'text',
              x: 4.8,
              y: 0.1,
              width: 1,
              height: 0.3,
              properties: {
                text: { value: 'product.units' },
                fontSize: 11,
                align: 'center'
              }
            }
          ]
        },

        // Footer
        {
          id: 'footer-line',
          type: 'line',
          x: 0.5,
          y: 9.8,
          width: 7.5,
          height: 0,
          style: { stroke: '#d1d5db', strokeWidth: 1 }
        },
        {
          id: 'footer-info',
          type: 'text',
          x: 0.5,
          y: 9.9,
          width: 7.5,
          height: 0.3,
          properties: {
            text: { value: '`Generated on ${generatedDate} by ${author}`' },
            fontSize: 9,
            color: '#6b7280',
            align: 'center'
          }
        }
      ]
    }
  }
];

export function getDemoTemplates(): DemoTemplate[] {
  return demoTemplates;
}

export function getDemoTemplateById(id: string): DemoTemplate | undefined {
  return demoTemplates.find(template => template.id === id);
}

export function getDemoTemplatesByCategory(category: string): DemoTemplate[] {
  return demoTemplates.filter(template => template.category === category);
}

export function getDemoCategories(): string[] {
  return [...new Set(demoTemplates.map(template => template.category))];
}