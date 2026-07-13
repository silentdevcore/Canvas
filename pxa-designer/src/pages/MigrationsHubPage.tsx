import React from 'react';
import { useNavigate } from 'react-router-dom';
import { FiCode, FiLayout, FiArrowRight } from 'react-icons/fi';
import AppHeader from '@/components/Layout/AppHeader';

/**
 * Migrations landing page. Two domains, each a dedicated area with Code + Designer/Datasource sub-tabs:
 *  - PDF Migration         → /migrations/pdf         (Code → PXA-compatible PDF · UI-Designer → PXA design)
 *  - Spreadsheet Migration → /migrations/spreadsheet (Code → PXA spreadsheet API · Datasource → workbook)
 */
const MigrationsHubPage: React.FC = () => {
  const navigate = useNavigate();

  const cards = [
    {
      id: 'pdf',
      to: '/migrations/pdf',
      icon: <FiCode />,
      title: 'PDF Migration',
      blurb: 'Move PDF work into PXA — Code Migration converts C# from a PDF library (iText, Apryse, '
        + 'Aspose, Syncfusion, Foxit, Spire, …) into compatible PDF code with a live PDF preview; UI-Designer '
        + 'Migration converts report-designer files (DevExpress, RDL/RDLC, ActiveReports, FastReport, '
        + 'Telerik) into an editable PXA design.',
      tag: '15 PDF libraries · 7 report designers',
    },
    {
      id: 'spreadsheet',
      to: '/migrations/spreadsheet',
      icon: <FiLayout />,
      title: 'Spreadsheet Migration',
      blurb: 'Move spreadsheet work into PXA — Code Migration converts C# from a spreadsheet library '
        + '(ClosedXML, EPPlus, GemBox, Aspose.Cells) into the PXA spreadsheet API with a grid preview; '
        + 'Datasource Migration imports a spreadsheet file (.xlsx/.xls/.csv) into the spreadsheet editor.',
      tag: '4 spreadsheet libraries · file import',
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
                Bring existing reports and PDF-generation code into Power Dox Automation. Choose a migration type below —
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
