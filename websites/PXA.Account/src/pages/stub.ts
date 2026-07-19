export function stubPage(title: string, description: string): string {
  return `
    <header class="account-page-header">
      <div>
        <p class="pxa-kicker">Customer workspace</p>
        <h1>${title}</h1>
        <p>${description}</p>
      </div>
    </header>
    <section class="account-section">
      <div><h2>Coming soon</h2><p>This area of the customer portal is being built out. Check back soon.</p></div>
    </section>
  `;
}
