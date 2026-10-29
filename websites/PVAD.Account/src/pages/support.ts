export function supportPage(): string {
  return `
    <header class="account-page-header">
      <div>
        <p class="pvad-kicker">Customer workspace</p>
        <h1>Support</h1>
        <p>Find help, request account or organization closure, and contact PVADE support.</p>
      </div>
    </header>
    <section class="account-section">
      <div>
        <h2>Account or organization closure</h2>
        <p>Manage closure requests for your personal account or your organization.</p>
        <a class="pvad-button pvad-button--secondary" href="/closure">Manage closure requests</a>
      </div>
    </section>
  `;
}
