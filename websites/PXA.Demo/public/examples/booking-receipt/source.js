export function createBookingReceipt(input) {
  return {
    documentType: 'BookingReceipt',
    title: 'Booking Receipt',
    reference: input.reference,
    customer: input.customer,
    date: input.date,
    items: input.items,
    total: input.total,
  };
}
