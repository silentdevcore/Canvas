import React from 'react';
import { useNavigate } from 'react-router-dom';
import { FiCode, FiLayout, FiArrowRight } from 'react-icons/fi';
import AppHeader from '@/components/Layout/AppHeader';

/**
 * Migrations landing page. Explains the two kinds of migration and links to each dedicated view:
 *  - Code Migration   → /migrations/code     (third-party PDF library C# → Canvas.Pdf code)
 *  - Designer Migration → /migrations/designer (report-designer files → editable Canvas designs)
 */
const MigrationsHubPage: React.FC = () => {
  const navigate = useNavigate();

  const cards = [
    {
      id: 'code',
      to: '/migrations/code',
      icon: <FiCode />,
      title: 'Code Migration',
      blurb: 'Convert C# source from a third-party PDF library — iText, Apryse, Aspose, Syncfusion, '
        + 'Foxit, Spire, and more — into equivalent Canvas.Pdf code, with a live PDF preview.',
      tag: '15 PDF frameworks',
    },
    {
      id: 'designer',
      to: '/migrations/designer',
      icon: <FiLayout />,
      title: 'Designer Migration',
      blurb: 'Convert a report-designer file — DevExpress XtraReports, RDL/RDLC (SSRS, Syncfusion), '
        + 'ActiveReports (.rdlx/.rpx), FastReport (.frx), Telerik (.trdx) — into an editable Canvas '
        + 'design you can open in the visual designer.',
      tag: '7 report designers',
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
