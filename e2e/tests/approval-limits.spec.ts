import { randomUUID } from 'node:crypto';
import { test, expect } from '@playwright/test';
import { InvoiceAppPage } from '../pages/InvoiceAppPage';

test('Manager over-limit approval shows a reason and preserves the entire pending invoice after reload', async ({ page }) => {
  const app = new InvoiceAppPage(page);
  const customer = `Manager boundary ${randomUUID()}`;
  await app.signInAsManager();
  const created = await app.createInvoice(customer, '50000.01');
  const before = await app.submitInvoice(created.id);
  expect(before).toMatchObject({
    id: created.id, customerName: customer, amount: 50000.01,
    status: 'PendingApproval', approvals: [], decidedBy: null, decidedAt: null, decisionNote: null,
  });
  await app.openApprovals();

  const rejected = await app.approveInvoice(created.id);

  expect(rejected.status()).toBe(409);
  await expect(app.decisionAlert()).toHaveText('Manager approval limit is 50,000; a Director is required.');
  const after = await app.reloadAndReadInvoice(created.id);
  expect(after).toEqual(before);
  await expect(app.invoiceRow(created.id).getByRole('cell', { name: 'Pending Approval', exact: true })).toBeVisible();
  await expect(app.invoiceRow(created.id).getByLabel(`Approval progress for ${created.id}`, { exact: true }))
    .toHaveText('0 of 1 approvals recorded');
  await app.openApprovals();
  await expect(page.getByRole('button', { name: `Approve invoice ${created.id}`, exact: true })).toBeVisible();
});
