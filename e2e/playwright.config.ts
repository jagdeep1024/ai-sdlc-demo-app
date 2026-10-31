import { defineConfig, devices } from '@playwright/test';

const baseURL = 'http://127.0.0.1:5097';

export default defineConfig({
  testDir: './tests',
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: `dotnet run --project ../backend/src/InvoiceApp.Api --no-launch-profile --urls ${baseURL}`,
    url: baseURL,
    reuseExistingServer: false,
    timeout: 120_000,
  },
});
