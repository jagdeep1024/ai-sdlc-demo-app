import { expect, type Locator, type Page, type Response } from '@playwright/test';

export interface InvoiceSnapshot {
  id: string;
  customerName: string;
  amount: number;
  status: string;
  createdAt: string;
  decidedBy: string | null;
  decidedAt: string | null;
  decisionNote: string | null;
  approvals: Array<{ username: string; role: string; approvedAt: string }>;
}

export class InvoiceAppPage {
  constructor(private readonly page: Page) {}

  async signInAsManager(): Promise<void> {
    await this.page.goto('/');
    await this.page.getByLabel('Username', { exact: true }).fill('demo.approver');
    await this.page.getByLabel('Password', { exact: true }).fill('ChangeMe123!');
    await this.page.getByRole('button', { name: 'Sign in', exact: true }).click();
    await expect(this.page.getByRole('tab', { name: 'Invoices', exact: true })).toBeVisible();
    await expect(this.page.getByText('demo.approver (Manager)', { exact: true })).toBeVisible();
  }

  async createInvoice(customerName: string, amount: string): Promise<InvoiceSnapshot> {
    await this.page.getByLabel('Customer name', { exact: true }).fill(customerName);
    await this.page.getByLabel('Amount', { exact: true }).fill(amount);
    const created = this.page.waitForResponse(response =>
      new URL(response.url()).pathname === '/api/invoices' && response.request().method() === 'POST');
    await this.page.getByRole('button', { name: 'Create invoice', exact: true }).click();
    const response = await created;
    expect(response.status()).toBe(201);
    const invoice = await response.json() as InvoiceSnapshot;
    await expect(this.invoiceRow(invoice.id)).toBeVisible();
    return invoice;
  }

  async submitInvoice(id: string): Promise<InvoiceSnapshot> {
    const submitted = this.page.waitForResponse(response =>
      new URL(response.url()).pathname === `/api/invoices/${id}/submit` && response.request().method() === 'POST');
    await this.page.getByRole('button', { name: `Submit invoice ${id} for approval`, exact: true }).click();
    const response = await submitted;
    expect(response.status()).toBe(200);
    await expect(this.invoiceRow(id).getByRole('cell', { name: 'Pending Approval', exact: true })).toBeVisible();
    return await response.json() as InvoiceSnapshot;
  }

  async openApprovals(): Promise<void> {
    await this.page.getByRole('tab', { name: 'Approvals', exact: true }).click();
  }

  async approveInvoice(id: string): Promise<Response> {
    const approval = this.page.waitForResponse(response =>
      new URL(response.url()).pathname === `/api/approvals/${id}/approve` && response.request().method() === 'POST');
    await this.page.getByRole('button', { name: `Approve invoice ${id}`, exact: true }).click();
    return await approval;
  }

  decisionAlert(): Locator {
    return this.page.getByRole('tabpanel', { name: 'Approvals', exact: true }).getByRole('alert');
  }

  invoiceRow(id: string): Locator {
    return this.page.getByRole('row').filter({ has: this.page.getByRole('cell', { name: id, exact: true }) });
  }

  async reloadAndReadInvoice(id: string): Promise<InvoiceSnapshot> {
    // Observe the UI's real authenticated refresh rather than reading browser storage.
    const refreshed = this.page.waitForResponse(response =>
      new URL(response.url()).pathname === '/api/invoices' && response.request().method() === 'GET');
    await this.page.reload();
    const response = await refreshed;
    expect(response.status()).toBe(200);
    const invoices = await response.json() as InvoiceSnapshot[];
    const invoice = invoices.find(item => item.id === id);
    expect(invoice, 'The same created invoice must remain after reload').toBeDefined();
    await expect(this.invoiceRow(id)).toBeVisible();
    return invoice!;
  }
}
