import React from 'react';
import { useNavigate } from 'react-router-dom';
import { FiCode, FiLayout, FiArrowRight } from 'react-icons/fi';
import AppHeader from '@/components/Layout/AppHeader';

/**
 * Migrations landing page. The two migration *types*, each a dedicated view:
 *  - Code Migration             → /migrations/code   (library C# → Canvas code: PDF + Spreadsheet)
 *  - DataSource / Format Migration → /migrations/format (a source file/format → Canvas design/model)
 */
const MigrationsHubPage: React.FC = () => {
  const navigate = useNavigate();

  const cards = [
    {
      id: 'code',
      to: '/migrations/code',
      icon: <FiCode />,
      title: 'Code Migration',
      blurb: 'Convert C# source that uses a third-party library into equivalent Canvas code — PDF '
        + 'libraries (iText, Apryse, Aspose, Syncfusion, Foxit, Spire, …) → Canvas.Pdf, and spreadsheet '
        + 'libraries (ClosedXML, EPPlus, GemBox, Aspose.Cells) → the Canvas spreadsheet API.',
      tag: '15 PDF + 4 spreadsheet libraries',
    },
    {
      id: 'format',
      to: '/migrations/format',
      icon: <FiLayout />,
      title: 'DataSource / Format Migration',
      blurb: 'Bring an existing file or format into Canvas — report-designer files (DevExpress, RDL/RDLC, '
        + 'ActiveReports, FastReport, Telerik, …) and documents (.pdf/.docx/.pptx/.odt/images) become '
        + 'editable Canvas designs; spreadsheets (.xlsx/.xls/.csv) open in the spreadsheet editor.',
      tag: 'report designers · documents · spreadsheets',
    },
  ];

  return (
    <div className="mgr-page">
      <AppHeader activePage="migrations" />

      <main className="mgr-main">
        <div className="mgr-heading">
          <div className="mgr-heading-left">
            <FiArrowRight className="mgr-heading-icon" />
            <div>
              <h1>Migrations</h1>
              <p>
                Bring existing reports and PDF-generation code into Canvas. Choose a migration type below —
                each opens a dedicated workspace where you paste a source file and convert it.
              </p>
            </div>
          </div>
        </div>

        <div className="mgr-hub-cards">
          {cards.map(card => (
            <button
              key={card.id}
              type="button"
              className="mgr-hub-card"
              onClick={() => navigate(card.to)}
            >
              <span className="mgr-hub-card-icon">{card.icon}</span>
              <span className="mgr-hub-card-body">
                <span className="mgr-hub-card-title">
                  {card.title}
                  <FiArrowRight className="mgr-hub-card-arrow" />
                </span>
                <span className="mgr-hub-card-blurb">{card.blurb}</span>
                <span className="mgr-hub-card-tag">{card.tag}</span>
              </span>
            </button>
          ))}
        </div>
      </main>
    </div>
  );
};

export default MigrationsHubPage;
