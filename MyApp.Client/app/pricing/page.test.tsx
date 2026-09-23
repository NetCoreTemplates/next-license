import {
  render,
  screen,
  cleanup,
  waitFor,
  fireEvent,
} from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import type { ReactNode } from "react";
import Pricing from "./page";
const mocks = vi.hoisted(() => ({
  api: vi.fn(),
  user: { displayName: "Buyer" } as { displayName: string } | undefined,
  admin: false,
}));
vi.mock("@servicestack/react", () => ({
  useClient: () => ({ api: mocks.api }),
}));
vi.mock("@/lib/auth", () => ({
  appAuth: () => ({ user: mocks.user, hasRole: () => mocks.admin }),
}));
vi.mock("@/components/layout", () => ({
  default: ({ children }: { children: ReactNode }) => <main>{children}</main>,
}));
afterEach(() => {
  cleanup();
  mocks.api.mockReset();
  mocks.user = { displayName: "Buyer" };
  mocks.admin = false;
});
const plan = { sku: "pro-12m-new", currency: "usd", unitAmountCents: 4900 };
it("shows only Free when no paid plans are approved", async () => {
  mocks.api.mockResolvedValue({ succeeded: true, response: { results: [] } });
  render(<Pricing />);
  await waitFor(() => expect(mocks.api).toHaveBeenCalledTimes(1));
  expect(screen.getByRole("heading", { name: "Free" })).toBeTruthy();
  expect(screen.queryByRole("heading", { name: "Pro · 12 months" })).toBeNull();
  expect(screen.queryByText("Your license details")).toBeNull();
});
it("lets customers choose a plan before entering details and sends the selected plan to checkout", async () => {
  mocks.api.mockResolvedValueOnce({
    succeeded: true,
    response: {
      results: [plan],
      agreement: { version: "1", bodyMarkdown: "License terms" },
    },
  });
  mocks.api.mockResolvedValueOnce({
    succeeded: false,
    error: { message: "Test checkout response" },
  });
  render(<Pricing />);
  const choose = await screen.findByRole("button", { name: "Choose Pro" });
  expect((choose as HTMLButtonElement).disabled).toBe(false);
  fireEvent.click(choose);
  expect(screen.queryByRole("heading", { name: "Pro · Lifetime" })).toBeNull();
  fireEvent.change(screen.getByLabelText("Licensee name"), {
    target: { value: "Buyer" },
  });
  fireEvent.click(screen.getByRole("checkbox"));
  fireEvent.click(screen.getByRole("button", { name: "Continue to Stripe" }));
  await waitFor(() => expect(mocks.api).toHaveBeenCalledTimes(2));
  expect(mocks.api.mock.calls[1][0]).toMatchObject({
    sku: plan.sku,
    licenseeName: "Buyer",
    seats: 1,
    acceptAgreement: true,
    agreementVersion: "1",
  });
});
it("explains missing license terms and gives administrators a setup link", async () => {
  mocks.admin = true;
  mocks.api.mockResolvedValue({
    succeeded: true,
    response: { results: [plan] },
  });
  render(<Pricing />);
  fireEvent.click(await screen.findByRole("button", { name: "Choose Pro" }));
  expect(
    screen.getByRole("heading", { name: "Checkout is not open yet" }),
  ).toBeTruthy();
  expect(
    screen
      .getAllByRole("link", { name: /Publish license terms/ })[0]
      .getAttribute("href"),
  ).toBe("/admin/agreements");
  expect(
    screen.queryByRole("button", { name: "Continue to Stripe" }),
  ).toBeNull();
  expect(mocks.api).toHaveBeenCalledTimes(1);
});
it("directs signed-out customers to sign in and preserves their selected plan", async () => {
  mocks.user = undefined;
  mocks.api.mockResolvedValue({
    succeeded: true,
    response: {
      results: [plan],
      agreement: { version: "1", bodyMarkdown: "Terms" },
    },
  });
  render(<Pricing />);
  fireEvent.click(await screen.findByRole("button", { name: "Choose Pro" }));
  expect(
    screen
      .getByRole("link", { name: "Sign in or create an account" })
      .getAttribute("href"),
  ).toBe("/signin?redirect=%2Fpricing%3Fplan%3Dpro-12m-new");
});
