export function createMasterDetailReport(input) {
  return {
    documentType: 'MasterDetailReport',
    title: input.title,
    groups: [
      {
        key: input.customer.id,
        label: input.customer.name,
        rows: input.orders,
      },
    ],
  };
}
