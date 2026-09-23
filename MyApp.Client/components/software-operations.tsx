"use client";
import MarkdownContent from "@/components/markdown-content";
import ReleaseNotes from '@/components/release-notes'
import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import {
  ArrowRight,
  Check,
  Download,
  ExternalLink,
  Github,
  KeyRound,
  Plus,
  Search,
  ShieldCheck,
  X,
} from "lucide-react";
import { client } from "@/lib/gateway";
import * as D from "@/lib/dtos";
import { downloadableBuilds, platformLabel } from "@/lib/software-downloads";
import IssueLicenseDialog from "./issue-license-dialog";
import OperationsShell, {
  PageHeading,
  Panel,
  StatusPill,
} from "./operations-shell";
export type Section =
  | "overview"
  | "catalog"
  | "releases"
  | "licenses"
  | "orders"
  | "settings"
  | "agreements";
/** Say what the customer gets, not what the database stores. */
const updateModeLabel = (mode?: string) =>
  mode === "Lifetime" ? "Lifetime updates" : mode === "ThroughDate" ? "Dated updates" : mode ?? "";

const titles: Record<Section, [string, string]> = {
  agreements: [
    "Clear terms. Lasting trust.",
    "Publish the license agreement customers accept before purchasing.",
  ],
  overview: [
    "Your next chapter starts here.",
    "A small console for a big idea. Everything you need to sell your software.",
  ],
  catalog: [
    "Great software. Simple pricing.",
    "Create your Stripe catalog, then choose which plans customers can buy.",
  ],
  releases: [
    "Ship something worth downloading.",
    "Published releases and platform builds, straight from your GitHub repository.",
  ],
  licenses: [
    "People behind your product.",
    "Find a customer and manage their perpetual license.",
  ],
  orders: [
    "Every purchase, in view.",
    "Find orders, review payments, and open the original transaction in Stripe.",
  ],
  settings: [
    "Connect the essentials.",
    "Your Stripe account. Your GitHub repository. Your software business.",
  ],
};
const name = (sku?: string) =>
  ({
    "pro-12m-new": "Pro · 12 months",
    "pro-lifetime-new": "Pro · Lifetime",
    "pro-12m-renewal": "Renew updates",
    "pro-lifetime-upgrade": "Upgrade to lifetime",
    "pro-edition-upgrade": "Enterprise upgrade",
  })[sku ?? ""] ?? sku;
const money = (n?: number, c = "usd") =>
  new Intl.NumberFormat("en", { style: "currency", currency: c }).format(
    (n ?? 0) / 100,
  );
export default function SoftwareOperations({ section }: { section: Section }) {
  const [dashboard, setDashboard] = useState<D.LicensingDashboardResponse>();
  const [setup, setSetup] = useState<D.SoftwareSetupResponse>();
  const [releases, setReleases] = useState<D.GitHubDownloadsResponse>();
  const [licenses, setLicenses] = useState<D.SoftwareLicense[]>([]);
  const [orders, setOrders] = useState<D.LicenseOrder[]>([]);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [busy, setBusy] = useState(false);
  const [query, setQuery] = useState("");
  const [skip, setSkip] = useState(0);
  const [lookup, setLookup] = useState(false);
  const [selected, setSelected] = useState<D.SoftwareLicense>();
  const [action, setAction] = useState("reissue");
  const [reason, setReason] = useState("");
  const [date, setDate] = useState("");
  const dialog = useRef<HTMLDialogElement>(null);
  const orderDialog = useRef<HTMLDialogElement>(null);
  const [selectedOrder, setSelectedOrder] = useState<D.LicenseOrder>();
  const [orderNotes, setOrderNotes] = useState("");
  async function load() {
    const [a, b] = await Promise.all([
      client.api(new D.GetLicensingDashboard()),
      client.api(new D.GetSoftwareSetup()),
    ]);
    if (a.succeeded) setDashboard(a.response);
    else setError(a.error?.message ?? "Administrator sign-in is required.");
    if (b.succeeded) setSetup(b.response);
  }
  async function search(offset = 0) {
    setBusy(true);
    setSkip(offset);
    if (section === "orders") {
      const a = await client.api(new D.SearchOrders({ query, skip: offset }));
      if (a.succeeded) setOrders(a.response?.results ?? []);
      else setError(a.error?.message ?? "Search failed.");
    } else {
      const a = await client.api(new D.SearchLicenses({ query, skip: offset }));
      if (a.succeeded) setLicenses(a.response?.results ?? []);
      else setError(a.error?.message ?? "Search failed.");
    }
    setBusy(false);
  }
  async function github() {
    setBusy(true);
    setError("");
    const a = await client.api(new D.GetGitHubDownloads());
    if (a.succeeded) setReleases(a.response);
    else setError(a.error?.message ?? "GitHub could not be reached.");
    setBusy(false);
  }
  useEffect(() => {
    void load();
    if (section === "releases" || section === "overview") void github();
    if (section === "licenses" || section === "orders") void search();
  }, [section]);
  async function provision() {
    setBusy(true);
    setError("");
    const a = await client.api(new D.CreateMissingStripe());
    if (a.succeeded) {
      setNotice(
        "Stripe prices are ready. Review and approve the plans you want to sell.",
      );
      await load();
    } else setError(a.error?.message ?? "Unable to create Stripe prices.");
    setBusy(false);
  }
  async function approve(p: D.PriceBook) {
    setBusy(true);
    const a = await client.api(
      new D.ApproveSoftwarePrice({ sku: p.sku, approved: !p.isActive }),
    );
    if (a.succeeded) await load();
    else setError(a.error?.message ?? "Unable to update plan.");
    setBusy(false);
  }
  async function support() {
    if (!selected) return;
    setBusy(true);
    setError("");
    const props = {
      id: selected.id,
      expectedBlobVersion: selected.blobVersion,
      reason,
    };
    const a =
      action === "lifetime"
        ? await client.api(new D.UpgradeToLifetimeUpdates(props))
        : action === "extend"
          ? await client.api(
              new D.ExtendUpdatesThrough({
                ...props,
                updatesThroughUtc: new Date(date + "T23:59:59Z").toISOString(),
              }),
            )
          : await client.api(new D.ReissueLicense(props));
    if (a.succeeded) {
      dialog.current?.close();
      setSelected(undefined);
      setNotice("License updated successfully.");
      await search();
    } else setError(a.error?.message ?? "Unable to update license.");
    setBusy(false);
  }
  async function reviewOrder() {
    if (!selectedOrder) return;
    setBusy(true);
    const a = await client.apiVoid(
      new D.ReviewLicenseOrder({ id: selectedOrder.id, notes: orderNotes }),
    );
    if (a.succeeded) {
      orderDialog.current?.close();
      setNotice("Order review recorded.");
      await search();
    } else setError(a.error?.message ?? "Unable to record review.");
    setBusy(false);
  }
  const choose = (l: D.SoftwareLicense) => {
    setSelected(l);
    setLookup(false);
    setReason("");
    dialog.current?.showModal();
  };
  return (
    <OperationsShell>
      <PageHeading
        title={titles[section][0]}
        description={titles[section][1]}
        action={
          <Link className="ops-button secondary" href="/download">
            View storefront <ExternalLink size={14} />
          </Link>
        }
      />
      {error && (
        <div className="ops-message error" role="alert">
          {error}
        </div>
      )}
      {notice && (
        <div className="ops-message" role="status">
          {notice}
        </div>
      )}
      {!dashboard && !error && (
        <Panel className="ops-empty">Loading your workspace…</Panel>
      )}
      {dashboard && (
        <>
          {section === "overview" && (
            <>
              <section className="ops-hero">
                <div>
                  <p className="ops-eyebrow">
                    BUILT BY YOU. READY FOR THE WORLD.
                  </p>
                  <h2>
                    Make great software.
                    <br />
                    <span>We’ll handle the keys.</span>
                  </h2>
                  <p>
                    Connect payments, publish your builds, and give your
                    customers a license that lasts.
                  </p>
                  <Link href="/admin/settings" className="ops-button">
                    Set up your business <ArrowRight size={16} />
                  </Link>
                </div>
                <div className="ops-hero-art">
                  <KeyRound size={66} strokeWidth={1} />
                  <span>PERPETUAL BY DESIGN</span>
                  <small>One purchase. Endless possibilities.</small>
                </div>
              </section>
              <div className="ops-metrics">
                {[
                  ["Active licenses", dashboard.activeLicenses],
                  ["Orders to review", dashboard.ordersRequiringReview],
                  ["Pending payments", dashboard.pendingStripeEvents],
                ].map(([label, value]) => (
                  <Panel key={label}>
                    <small>{label}</small>
                    <strong>{value ?? 0}</strong>
                    <span>Across your workspace</span>
                  </Panel>
                ))}
              </div>
              <div className="ops-section-title">
                <h2>Your launch checklist</h2>
                <span>THREE STEPS TO OPEN THE DOORS</span>
              </div>
              <div className="ops-grid">
                {[
                  [
                    "01",
                    "Connect your accounts",
                    "Add Stripe and your GitHub repository.",
                    "settings",
                    setup?.stripeConfigured && setup?.repository,
                  ],
                  [
                    "02",
                    "Approve your pricing",
                    "Create prices and choose your paid plans.",
                    "catalog",
                    dashboard.prices?.some((p) => p.isActive),
                  ],
                  [
                    "03",
                    "Publish your software",
                    "Your GitHub builds become public downloads.",
                    "releases",
                    releases?.results?.some(r => downloadableBuilds(r.assets).length > 0),
                  ],
                ].map(([n, title, body, path, done]) => (
                  <Link href={"/admin/" + path} key={String(n)}>
                    <Panel className="ops-step">
                      <span className="ops-step-number">
                        {done ? <Check size={20} /> : n}
                      </span>
                      <h3>{title}</h3>
                      <p>{body}</p>
                      <ArrowRight size={18} />
                    </Panel>
                  </Link>
                ))}
              </div>
            </>
          )}
          {section === "catalog" && (
            <>
              <div className="ops-toolbar">
                <div>
                  <StatusPill
                    tone={setup?.stripeConfigured ? "green" : "amber"}
                  >
                    {setup?.stripeConfigured
                      ? `Stripe ${setup.liveMode ? "live" : "test"} mode`
                      : "Stripe not connected"}
                  </StatusPill>
                  <p>Creating prices never publishes a plan automatically.</p>
                </div>
                <button
                  className="ops-button"
                  disabled={busy || !setup?.stripeConfigured}
                  onClick={provision}
                >
                  <Plus size={16} />
                  {busy ? "Working…" : "Create Missing Stripe"}
                </button>
              </div>
              <div className="ops-grid">
                {dashboard.prices?.map((p) => (
                  <Panel key={p.sku} className="ops-product">
                    <StatusPill tone={p.isActive ? "green" : "slate"}>
                      {p.isActive ? "Approved" : "Draft"}
                    </StatusPill>
                    <h2>{name(p.sku)}</h2>
                    <div className="ops-price">
                      {p.unitAmountCents
                        ? money(p.unitAmountCents, p.currency)
                        : "Set a price"}
                      <small>one-time / seat</small>
                    </div>
                    <form
                      className="ops-price-edit"
                      onSubmit={async (e) => {
                        e.preventDefault();
                        const f = new FormData(e.currentTarget);
                        setBusy(true);
                        const a = await client.api(
                          new D.SetSoftwarePrice({
                            sku: p.sku,
                            unitAmountCents: Math.round(
                              Number(f.get("amount")) * 100,
                            ),
                            currency: String(f.get("currency")).toLowerCase(),
                          }),
                        );
                        if (a.succeeded) {
                          setNotice(
                            "Price saved as a draft. Create its Stripe price, then approve it.",
                          );
                          await load();
                        } else
                          setError(a.error?.message ?? "Unable to save price.");
                        setBusy(false);
                      }}
                    >
                      <label>
                        Unit price
                        <input
                          aria-label={`Price for ${name(p.sku)}`}
                          name="amount"
                          type="number"
                          min="0.01"
                          max="1000000"
                          step="0.01"
                          required
                          defaultValue={(p.unitAmountCents ?? 0) / 100}
                        />
                      </label>
                      <label>
                        Currency
                        <input
                          name="currency"
                          aria-label="Currency"
                          required
                          pattern="[A-Za-z]{3}"
                          maxLength={3}
                          defaultValue={p.currency ?? "usd"}
                        />
                      </label>
                      <button disabled={busy} className="ops-text-button">
                        Save
                      </button>
                    </form>
                    <p>
                      {p.sku?.endsWith("-new")
                        ? "Available to new customers when approved."
                        : "Available to existing license holders when approved."}
                    </p>
                    <div className="ops-product-status">
                      <Check size={15} />
                      {p.stripePriceId
                        ? "Stripe price connected"
                        : "Awaiting Stripe price"}
                    </div>
                    <button
                      className="ops-button secondary"
                      disabled={busy || !p.stripePriceId}
                      onClick={() => approve(p)}
                    >
                      {p.isActive ? "Remove from sale" : "Approve for sale"}
                      <ArrowRight size={15} />
                    </button>
                  </Panel>
                ))}
              </div>
              <p className="ops-footnote">
                {!dashboard.agreements?.length && (
                  <>
                    <Link className="ops-text-button" href="/admin/agreements">
                      Publish your license terms
                    </Link>{" "}
                    before accepting purchases.{" "}
                  </>
                )}
                Free is always available. Only approved paid plans appear on
                your pricing page.
              </p>
            </>
          )}
          {section === "agreements" && (
            <>
              <Panel className="ops-agreement">
                <h2>Publish a license agreement</h2>
                <p className="ops-footnote">
                  Published versions are immutable. To change your terms,
                  publish a new version. Checkout records the version each
                  customer accepted.
                </p>
                <form
                  onSubmit={async (e) => {
                    e.preventDefault();
                    const f = new FormData(e.currentTarget);
                    setBusy(true);
                    const a = await client.api(
                      new D.PublishLicenseAgreement({
                        version: String(f.get("version")),
                        bodyMarkdown: String(f.get("body")),
                      }),
                    );
                    if (a.succeeded) {
                      setNotice(
                        "Agreement published. New checkouts will use this version.",
                      );
                      await load();
                    } else
                      setError(
                        a.error?.message ?? "Unable to publish agreement.",
                      );
                    setBusy(false);
                  }}
                >
                  <label>
                    Version
                    <input
                      required
                      name="version"
                      maxLength={60}
                      pattern="[A-Za-z0-9.-]+"
                      placeholder="e.g. 2026-09"
                    />
                  </label>
                  <label>
                    Agreement text
                    <textarea
                      required
                      name="body"
                      maxLength={100000}
                      rows={12}
                      placeholder="Enter the license terms for your software…"
                    />
                  </label>
                  <button className="ops-button" disabled={busy}>
                    Publish agreement
                  </button>
                </form>
              </Panel>
              {dashboard.agreements?.map((a) => (
                <Panel key={a.version} className="ops-agreement mt-5">
                  <h2>Version {a.version}</h2>
                  <p className="ops-footnote">
                    Published{" "}
                    {a.effectiveAtUtc &&
                      new Date(a.effectiveAtUtc).toLocaleDateString()}
                  </p>
                  <details>
                    <summary className="ops-text-button">
                      Read agreement
                    </summary>
                    <MarkdownContent>{a.bodyMarkdown}</MarkdownContent>
                  </details>
                </Panel>
              ))}
            </>
          )}
          {section === "settings" && (
            <div className="ops-settings">
              {[
                [
                  "Stripe",
                  "Accept one-time payments",
                  setup?.stripeConfigured,
                  "Stripe__SecretKey",
                  "Set your secret key in the server .env file. Set Stripe__WebhookSecret for fulfillment and Stripe__LiveMode=true only for a live account.",
                ],
                [
                  "GitHub",
                  "Your releases become downloads",
                  !!setup?.repository,
                  "Licensing__GitHubRepository",
                  setup?.repository ??
                    "Set owner/repository in .env. Example: NetCoreApps/acme-studio",
                ],
                [
                  "License signing",
                  "Unlock Pro, even offline",
                  setup?.signingConfigured,
                  "Licensing__LicensePrivateKeyPem",
                  "Configure the ES256 private signing key on your server. Bundle only the matching public key in your desktop app.",
                ],
              ].map(([title, subtitle, ready, key, body]) => (
                <Panel key={String(title)} className="ops-integration">
                  <div className="ops-integration-icon">
                    {title === "GitHub" ? <Github /> : <ShieldCheck />}
                  </div>
                  <div>
                    <h2>{title}</h2>
                    <p>{subtitle}</p>
                    <code>{key}</code>
                    <p>{body}</p>
                  </div>
                  <StatusPill tone={ready ? "green" : "amber"}>
                    {ready ? "Configured" : "Needs setup"}
                  </StatusPill>
                </Panel>
              ))}
              <Panel className="p-6">
                <h3>Keep secrets on your server</h3>
                <p className="ops-footnote">
                  Restart the backend after changing .env. Environment variables
                  take precedence.{" "}
                  {setup?.webhookConfigured
                    ? "Stripe webhook secret is configured."
                    : "Stripe webhook secret still needs configuration."}
                </p>
              </Panel>
            </div>
          )}
          {section === "releases" && (
            <>
              <div className="ops-toolbar">
                <div>
                  <Github size={24} />
                  <p>
                    {setup?.repository ??
                      "Connect a repository in Integrations"}{" "}
                    · refreshes every two minutes
                  </p>
                </div>
                <button
                  className="ops-button"
                  disabled={busy || !setup?.repository}
                  onClick={github}
                >
                  {busy ? "Checking GitHub…" : "Refresh releases"}
                </button>
              </div>
              {releases?.results?.length === 0 && (
                <Empty
                  title="Your next release belongs here."
                  body="Publish a GitHub release with platform builds attached. They will appear here and on your download page."
                />
              )}
              {releases?.results?.map((r) => (
                <Panel className="ops-release" key={r.tag}>
                  <div className="ops-release-title">
                    <span className="ops-integration-icon">
                      <Download />
                    </span>
                    <div>
                      <h2>{r.name || r.tag}</h2>
                      <p>
                        {r.tag} ·{" "}
                        {r.publishedAt &&
                          new Date(r.publishedAt).toLocaleDateString()}
                      </p>
                    </div>
                    <StatusPill tone={r.prerelease ? "amber" : "green"}>
                      {r.prerelease ? "Preview" : "Published"}
                    </StatusPill>
                  </div>
                  <ReleaseNotes>{r.notes}</ReleaseNotes>
                  <div className="ops-assets">
                    {downloadableBuilds(r.assets).map((a) => (
                      <a
                        className="ops-button secondary"
                        href={a.url}
                        key={a.url}
                      >
                        <Download size={14} />
                        <span>
                          {platformLabel(a.name)}
                          <small className="block text-[10px] mt-1">
                            {a.name}
                          </small>
                        </span>
                        <small>{((a.size ?? 0) / 1048576).toFixed(1)} MB</small>
                      </a>
                    ))}
                  </div>
                </Panel>
              ))}
            </>
          )}
          {(section === "licenses" || section === "orders") && (
            <>
              <div className="ops-toolbar">
                <form
                  className="ops-search-form"
                  onSubmit={(e) => {
                    e.preventDefault();
                    void search();
                  }}
                >
                  <div className="ops-search">
                    <Search size={18} />
                    <input
                      aria-label="Search customers"
                      placeholder={
                        section === "orders"
                          ? "Search customer or order number…"
                          : "Search customer or organization…"
                      }
                      value={query}
                      onChange={(e) => setQuery(e.target.value)}
                    />
                  </div>
                  <button className="ops-button" disabled={busy}>
                    Search
                  </button>
                </form>
                {section === "licenses" && (
                  <IssueLicenseDialog
                    onIssued={() => {
                      setNotice("License issued successfully.");
                      void search();
                    }}
                  />
                )}
                {section === "licenses" && (
                  <button
                    type="button"
                    className="ops-button secondary"
                    onClick={() => {
                      setLookup(true);
                      dialog.current?.showModal();
                    }}
                  >
                    Find a license
                  </button>
                )}
              </div>
              <Panel className="ops-table-wrap">
                <table className="ops-table">
                  <thead>
                    <tr>
                      <th>Customer</th>
                      <th>{section === "orders" ? "Order" : "Entitlement"}</th>
                      <th>Status</th>
                      <th>Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {section === "licenses"
                      ? licenses.map((l) => (
                          <tr key={l.id}>
                            <td>
                              <strong>{l.licenseeName}</strong>
                              <small>
                                {l.licenseeOrganization || "Individual license"}
                              </small>
                            </td>
                            <td>
                              {l.edition} · {updateModeLabel(l.updateMode)}
                            </td>
                            <td>
                              <StatusPill>{l.status}</StatusPill>
                            </td>
                            <td>
                              <button
                                className="ops-text-button"
                                onClick={() => choose(l)}
                              >
                                Manage license →
                              </button>
                            </td>
                          </tr>
                        ))
                      : orders.map((o) => (
                          <tr key={o.id}>
                            <td>
                              <strong>{o.licenseeName}</strong>
                              <small>
                                {money(
                                  o.finalAmountCents ?? o.expectedAmountCents,
                                  o.currency,
                                )}
                              </small>
                            </td>
                            <td>{o.orderNumber}</td>
                            <td>
                              <StatusPill
                                tone={o.requiresReview ? "amber" : "green"}
                              >
                                {o.status}
                              </StatusPill>
                            </td>
                            <td>
                              {o.stripePaymentIntentId ? (
                                <a
                                  className="ops-text-button"
                                  href={`https://dashboard.stripe.com/${setup?.liveMode ? "" : "test/"}payments/${o.stripePaymentIntentId}`}
                                  target="_blank"
                                  rel="noreferrer"
                                >
                                  View in Stripe ↗
                                </a>
                              ) : (
                                "Awaiting payment"
                              )}
                              {o.requiresReview && (
                                <button
                                  className="ops-text-button block mt-2"
                                  onClick={() => {
                                    setSelectedOrder(o);
                                    setOrderNotes("");
                                    orderDialog.current?.showModal();
                                  }}
                                >
                                  Complete review →
                                </button>
                              )}
                            </td>
                          </tr>
                        ))}
                  </tbody>
                </table>
                {!(section === "orders" ? orders : licenses).length && (
                  <Empty
                    title={query ? "No matching records" : "A fresh start."}
                    body={
                      query
                        ? "Try a different customer name."
                        : "Your customers will appear here after their first purchase."
                    }
                  />
                )}
              </Panel>
              <div className="ops-pagination">
                <button
                  disabled={busy || skip === 0}
                  onClick={() => search(Math.max(0, skip - 50))}
                >
                  ← Previous
                </button>
                <span>Page {skip / 50 + 1}</span>
                <button
                  disabled={
                    busy ||
                    (section === "orders" ? orders : licenses).length < 50
                  }
                  onClick={() => search(skip + 50)}
                >
                  Next →
                </button>
              </div>
            </>
          )}
        </>
      )}
      <dialog ref={orderDialog} className="ops-dialog">
        <button
          className="ops-close"
          aria-label="Close order review"
          onClick={() => orderDialog.current?.close()}
        >
          <X />
        </button>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            void reviewOrder();
          }}
        >
          <p className="ops-eyebrow">Order review</p>
          <h2>{selectedOrder?.orderNumber}</h2>
          <p>
            {selectedOrder?.licenseeName} ·{" "}
            {money(selectedOrder?.finalAmountCents, selectedOrder?.currency)}
          </p>
          <label>
            Review notes
            <textarea
              required
              maxLength={2000}
              value={orderNotes}
              onChange={(e) => setOrderNotes(e.target.value)}
            />
          </label>
          {error && <p role="alert">{error}</p>}
          <button className="ops-button" disabled={busy}>
            Complete review
          </button>
        </form>
      </dialog>
      <dialog
        ref={dialog}
        className="ops-dialog"
        onCancel={() => setSelected(undefined)}
      >
        <button
          aria-label="Close dialog"
          className="ops-close"
          onClick={() => dialog.current?.close()}
        >
          <X />
        </button>
        {lookup ? (
          <>
            <h2>Find a license</h2>
            <p>
              Search by customer name or organization, then select a license.
            </p>
            <form
              className="ops-search"
              onSubmit={(e) => {
                e.preventDefault();
                void search();
              }}
            >
              <Search size={18} />
              <input
                autoFocus
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                aria-label="Find license"
              />
              <button className="ops-text-button">Search</button>
            </form>
            {licenses.map((l) => (
              <button
                className="ops-lookup-row"
                key={l.id}
                onClick={() => choose(l)}
              >
                <span>
                  <strong>{l.licenseeName}</strong>
                  <small>
                    {l.licenseeOrganization} · {l.edition} · {updateModeLabel(l.updateMode)}
                  </small>
                </span>
                <ArrowRight size={16} />
              </button>
            ))}
            {!licenses.length && <p>No licenses found.</p>}
          </>
        ) : (
          <form
            onSubmit={(e) => {
              e.preventDefault();
              void support();
            }}
          >
            <p className="ops-eyebrow">Customer support</p>
            <h2>{selected?.licenseeName}</h2>
            <p>
              {selected?.edition} · {updateModeLabel(selected?.updateMode)}
            </p>
            <label>
              Action
              <select
                value={action}
                onChange={(e) => setAction(e.target.value)}
              >
                <option value="reissue">Reissue license</option>
                <option value="extend">Extend update coverage</option>
                <option value="lifetime">Grant lifetime updates</option>
              </select>
            </label>
            {action === "extend" && (
              <label>
                Updates through
                <input
                  type="date"
                  required
                  value={date}
                  onChange={(e) => setDate(e.target.value)}
                />
              </label>
            )}
            <label>
              Reason
              <textarea
                required
                maxLength={2000}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
              />
            </label>
            <p className="ops-footnote">
              Changes preserve perpetual offline access. The customer can
              download their updated license from their account.
            </p>
            {error && (
              <div role="alert" className="ops-message error">
                <span>{error}</span>
                </div>
            )}
            <button className="ops-button" disabled={busy}>
              {busy ? "Saving…" : "Update license"}
            </button>
          </form>
        )}
      </dialog>
    </OperationsShell>
  );
}
function Empty({ title, body }: { title: string; body: string }) {
  return (
    <div className="ops-empty">
      <KeyRound size={30} />
      <h2>{title}</h2>
      <p>{body}</p>
    </div>
  );
}
