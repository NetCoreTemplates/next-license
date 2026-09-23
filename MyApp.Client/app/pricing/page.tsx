"use client";
import MarkdownContent from "@/components/markdown-content";
import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import {
  ArrowRight,
  Check,
  ShieldCheck,
  Infinity as InfinityIcon,
  CalendarDays,
  Sparkles,
} from "lucide-react";
import { useClient } from "@servicestack/react";
import {
  GetLicensePricing,
  CreateLicenseCheckout,
  LicensePricingResponse,
} from "@/lib/dtos";
import Layout from "@/components/layout";
import { appAuth } from "@/lib/auth";
export default function Pricing() {
  // useClient returns a new state object on each render; mount effects must not depend on its identity.
  const client = useClient();
  const { user, hasRole } = appAuth();
  const [selectedSku, setSelectedSku] = useState<string>();
  const checkout = useRef<HTMLElement>(null);
  useEffect(() => {
    if (selectedSku) {
      checkout.current?.scrollIntoView?.({
        behavior: "smooth",
        block: "center",
      });
      checkout.current?.focus({ preventScroll: true });
    }
  }, [selectedSku]);
  const [pricing, setPricing] = useState<LicensePricingResponse>();
  const [error, setError] = useState("");
  const [name, setName] = useState("");
  const [organization, setOrganization] = useState("");
  const [seats, setSeats] = useState(1);
  const [accepted, setAccepted] = useState(false);
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    void (async () => {
      const api = await client.api(new GetLicensePricing());
      if (api.succeeded) setPricing(api.response);
      else setError(api.error?.message ?? "Pricing is unavailable.");
    })();
  }, []);
  useEffect(() => {
    const plan = new URLSearchParams(window.location.search).get("plan");
    if (
      plan &&
      pricing?.results?.some((p) => p.sku === plan && p.sku.endsWith("-new"))
    )
      setSelectedSku(plan);
  }, [pricing]);
  async function buy(sku: string) {
    if (!canBuy) {
      setError(
        "Enter your name, choose a valid seat count, and accept the license agreement.",
      );
      return;
    }
    setBusy(true);
    setError("");
    const api = await client.api(
      new CreateLicenseCheckout({
        sku,
        licenseeName: name,
        licenseeOrganization: organization || undefined,
        seats,
        acceptAgreement: accepted,
        agreementVersion: pricing?.agreement?.version,
      }),
    );
    setBusy(false);
    if (api.succeeded && api.response!.url)
      window.location.assign(api.response!.url);
    else setError(api.error?.message ?? "Checkout could not be started.");
  }
  const canBuy =
    accepted &&
    !!name.trim() &&
    Number.isInteger(seats) &&
    seats > 0 &&
    seats <= 10000 &&
    !!pricing?.agreement;
  return (
    <Layout>
      <div className="pricing-page">
        <div className="pricing-heading">
          <p className="eyebrow">
            A little investment. A long-term relationship.
          </p>
          <h1>
            Your tools.
            <br />
            <span>Your kind of forever.</span>
          </h1>
          <p>
            Start free. Choose how you want to grow.
            <br />
            Every paid license is a one-time purchase with perpetual use.
          </p>
        </div>
        {error && (
          <p role="alert" className="studio-alert">
            {error}
          </p>
        )}
        {pricing?.results?.some((p) => p.sku?.endsWith("-new")) &&
          !pricing.agreement &&
          hasRole("Admin") && (
            <div className="studio-alert" role="status">
              Your Stripe price is ready. Publish a license agreement to enable
              checkout.{" "}
              <Link
                className="underline font-semibold"
                href="/admin/agreements"
              >
                Publish license terms →
              </Link>
            </div>
          )}
        <div className="pricing-cards">
          <section className="price-card">
            <span className="price-icon">
              <Sparkles size={22} />
            </span>
            <h2>Free</h2>
            <p>A place to start your next great idea.</p>
            <div className="price-amount">
              $0<span>always free</span>
            </div>
            <Link href="/download" className="studio-button secondary">
              Explore downloads <ArrowRight size={15} />
            </Link>
            <ul>
              {[
                "Free editing workspace",
                "Public software downloads",
                "No license required",
              ].map((x) => (
                <li key={x}>
                  <Check size={15} />
                  {x}
                </li>
              ))}
            </ul>
          </section>
          {[false, true]
            .filter((lifetime) =>
              pricing?.results?.some(
                (p) =>
                  p.sku === (lifetime ? "pro-lifetime-new" : "pro-12m-new"),
              ),
            )
            .map((lifetime) => {
              const sku = lifetime ? "pro-lifetime-new" : "pro-12m-new";
              const price = pricing?.results?.find((p) => p.sku === sku);
              // Highlight the most complete paid tier on offer.
              const featured =
                lifetime ||
                !pricing?.results?.some((p) => p.sku === "pro-lifetime-new");
              const cents = (price?.unitAmountCents ?? 0) * seats;
              return (
                <section
                  key={sku}
                  className={`price-card ${featured ? "price-featured" : ""}`}
                >
                  {lifetime && (
                    <span className="price-ribbon">THE LONG VIEW</span>
                  )}
                  <span className="price-icon">
                    {lifetime ? (
                      <InfinityIcon size={24} />
                    ) : (
                      <CalendarDays size={22} />
                    )}
                  </span>
                  <h2>{lifetime ? "Pro · Lifetime" : "Pro · 12 months"}</h2>
                  <p>
                    {lifetime
                      ? "One decision. Every future Pro version."
                      : "Keep your features. Renew on your terms."}
                  </p>
                  <div className="price-amount">
                    {price ? (
                      new Intl.NumberFormat("en", {
                        style: "currency",
                        currency: price.currency ?? "USD",
                        minimumFractionDigits: cents % 100 === 0 ? 0 : 2,
                      }).format(cents / 100)
                    ) : (
                      <span className="price-unavailable">
                        {pricing ? "Coming soon" : "Loading price…"}
                      </span>
                    )}
                    <span>
                      {price
                        ? `one-time · ${seats} seat${seats === 1 ? "" : "s"}`
                        : "Purchasing is not open yet"}
                    </span>
                  </div>
                  <button
                    disabled={busy || !price}
                    onClick={() => {
                      setError("");
                      setSelectedSku(sku);
                    }}
                    className={`studio-button ${featured ? "primary" : "secondary"}`}
                  >
                    {selectedSku === sku
                      ? "Selected · Continue below"
                      : price
                        ? "Choose Pro"
                        : "Not yet available"}
                    <ArrowRight size={15} />
                  </button>
                  <ul>
                    {[
                      lifetime
                        ? "Lifetime Pro version updates"
                        : "12 months of Pro version updates",
                      "Use covered versions forever",
                      "Use your paid features offline",
                      lifetime
                        ? "No renewal needed"
                        : "Renew or upgrade when you choose",
                    ].map((x) => (
                      <li key={x}>
                        <Check size={15} />
                        {x}
                      </li>
                    ))}
                  </ul>
                </section>
              );
            })}
        </div>
        <p className="pricing-assurance">
          <ShieldCheck size={16} /> Secure checkout with Stripe. No recurring
          charges. Seats are recorded on the honour system.
        </p>
        {selectedSku && (
          <section
            ref={checkout}
            tabIndex={-1}
            aria-label="Complete your purchase"
            className="checkout-details"
          >
            <div>
              <p className="eyebrow">Make it yours</p>
              <h2>
                {selectedSku === "pro-lifetime-new"
                  ? "Pro · Lifetime"
                  : "Pro · 12 months"}
              </h2>
              <p style={{ marginTop: 12, fontWeight: 600 }}>Your license details</p>
              <p>
                A verified account is required. License files are signed, not
                encrypted: your name, organization, edition, seats, and update
                entitlement are readable by anyone holding the file.
              </p>
            </div>
            {!pricing?.agreement ? (
              <div role="status" style={{ display: 'grid', gap: 14, alignContent: 'start', justifyItems: 'start' }}>
                <h3 style={{ fontSize: 'var(--t-lg)' }}>
                  Checkout is not open yet
                </h3>
                <p style={{ fontSize: 'var(--t-sm)', color: 'var(--ink-muted)', lineHeight: 1.8 }}>
                  The license agreement has not been published. Please check
                  back once the store is ready.
                </p>
                {hasRole("Admin") && (
                  <Link
                    href="/admin/agreements"
                    className="studio-button primary"
                  >
                    Publish license terms <ArrowRight size={15} />
                  </Link>
                )}
              </div>
            ) : !user ? (
              <div style={{ display: 'grid', gap: 14, alignContent: 'start', justifyItems: 'start' }}>
                <h3 style={{ fontSize: 'var(--t-lg)' }}>Sign in to continue</h3>
                <p style={{ fontSize: 'var(--t-sm)', color: 'var(--ink-muted)' }}>Your license will be saved to your account.</p>
                <Link
                  className="studio-button primary"
                  href={`/signin?redirect=${encodeURIComponent("/pricing?plan=" + selectedSku)}`}
                >
                  Sign in or create an account <ArrowRight size={15} />
                </Link>
              </div>
            ) : (
              <form
                style={{ display: 'grid', gap: 18, alignContent: 'start' }}
                onSubmit={(e) => {
                  e.preventDefault();
                  void buy(selectedSku);
                }}
              >
                <label className="studio-field">
                  <span>Licensee name</span>
                  <input
                    required
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    maxLength={200}
                    placeholder="The name printed in your license"
                  />
                </label>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 96px', gap: 14 }}>
                  <label className="studio-field">
                    <span>Organization</span>
                    <input
                      value={organization}
                      onChange={(e) => setOrganization(e.target.value)}
                      maxLength={200}
                      placeholder="Optional"
                    />
                  </label>
                  <label className="studio-field">
                    <span>Seats</span>
                    <input
                      required
                      type="number"
                      min={1}
                      max={10000}
                      step={1}
                      value={seats}
                      onChange={(e) => setSeats(Number(e.target.value))}
                    />
                  </label>
                </div>
                <details style={{ border: '1px solid var(--line)', borderRadius: 'var(--r-md)', background: 'var(--surface-sunken)', padding: 16, fontSize: 'var(--t-sm)' }}>
                  <summary style={{ cursor: 'pointer', fontWeight: 600 }}>
                    License agreement · <span className="fact">{pricing.agreement.version}</span>
                  </summary>
                  <MarkdownContent>{pricing.agreement.bodyMarkdown}</MarkdownContent>
                </details>
                <label style={{ display: 'flex', alignItems: 'flex-start', gap: 10, fontSize: 'var(--t-sm)', lineHeight: 1.7, color: 'var(--ink-muted)' }}>
                  <input
                    required
                    type="checkbox"
                    style={{ marginTop: 4 }}
                    checked={accepted}
                    onChange={(e) => setAccepted(e.target.checked)}
                  />
                  I accept this agreement and understand that my paid feature
                  entitlement is perpetual.
                </label>
                <button
                  className="studio-button primary"
                  disabled={busy}
                  type="submit"
                  style={{ justifySelf: 'start' }}
                >
                  {busy ? "Opening checkout…" : "Continue to Stripe"}
                  <ArrowRight size={15} />
                </button>
                <p style={{ fontSize: 'var(--t-xs)', color: 'var(--ink-subtle)' }}>
                  One-time payment. You’ll review your purchase in Stripe before
                  paying.
                </p>
                {error && (
                  <div role="alert" className="studio-alert error">
                    <span>{error}</span>
                  </div>
                )}
              </form>
            )}
          </section>
        )}
        <section className="pricing-faq">
          <p className="eyebrow">Clear by design</p>
          <h2>A license that makes sense.</h2>
          {[
            [
              "What happens after my 12 months?",
              "Keep using Pro in covered app versions forever. Renew to unlock Pro in builds released after your cutoff.",
            ],
            [
              "Can I keep installing updates?",
              "Yes. Every software build is public. Your license determines whether Pro is unlocked in that version.",
            ],
            [
              "Do I need an internet connection?",
              "No. Paid license verification happens entirely offline. Optional refresh can retrieve an updated license after renewal.",
            ],
            [
              "Can I switch to lifetime later?",
              "Yes. Upgrade your dated Pro license from My licenses. Your license identity stays the same.",
            ],
          ].map(([q, a]) => (
            <details key={q}>
              <summary>{q}</summary>
              <p>{a}</p>
            </details>
          ))}
        </section>
      </div>
    </Layout>
  );
}
