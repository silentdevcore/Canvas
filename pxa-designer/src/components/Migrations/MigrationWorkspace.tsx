import React from 'react';
import MigrationTabs, { type MigrationTab } from './MigrationTabs';

interface Props {
  tabs: MigrationTab[];
  children: React.ReactNode;
}

/** Shared product shell. Migration kinds keep their own typed editors and artifacts inside it. */
const MigrationWorkspace: React.FC<Props> = ({ tabs, children }) => (
  <div className="mgr-page">
    <MigrationTabs tabs={tabs} />
    {children}
  </div>
);

export default MigrationWorkspace;
