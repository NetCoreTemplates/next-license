"use client";
import { useRef, useState } from "react";
import { ArrowLeft, ArrowRight, Search, X } from "lucide-react";
import { client } from "@/lib/gateway";
import {
  SearchLicenseCustomers,
  LicenseCustomer,
  IssueLicense,
  Edition,
  UpdateMode,
} from "@/lib/dtos";
export default function IssueLicenseDialog({
  onIssued,
}: {
  onIssued: () => void;
}) {
  const dialog = useRef<HTMLDialogElement>(null);
  const searchInput = useRef<HTMLInputElement>(null);
  const [query, setQuery] = useState("");
  const [customers, setCustomers] = useState<LicenseCustomer[]>([]);
  const [customer, setCustomer] = useState<LicenseCustomer>();
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [lifetime, setLifetime] = useState(false);
  const [skip, setSkip] = useState(0);
  async function search(offset = 0) {
    setSkip(offset);
    setBusy(true);
    setError("");
    const a = await client.api(
      new SearchLicenseCustomers({ query, skip: offset }),
    );
    if (a.succeeded) setCustomers(a.response?.results ?? []);
    else setError(a.error?.message ?? "Unable to find customers.");
    setBusy(false);
  }
  return (
    <>
      <button
        type="button"
        className="ops-button secondary"
        onClick={() => {
          setCustomer(undefined);
          setError("");
          dialog.current?.showModal();
          searchInput.current?.focus();
          void search();
        }}
      >
        Issue a license
      </button>
      <dialog
        ref={dialog}
        className={`ops-dialog ops-customer-dialog${customer ? "" : " is-looking-up"}`}
      >
        <button
          className="ops-close"
          aria-label="Close issue license"
          onClick={() => dialog.current?.close()}
        >
          <X />
        </button>
        <p className="ops-eyebrow">Administrative license</p>
        <h2>{customer ? "Make it theirs." : "Choose a customer"}</h2>
        {error && (
          <div role="alert" className="ops-message error">
            <span>{error}</span>
          </div>
        )}
        {!customer ? (
          <div className="ops-customer-lookup">
            <p>Search registered customers by name or email.</p>
            <form
              className="ops-search"
              onSubmit={(e) => {
                e.preventDefault();
                void search();
              }}
            >
              <Search size={16} />
              <input
                ref={searchInput}
                aria-label="Search registered customers"
                placeholder="Name or email"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
              />
              <button disabled={busy} className="ops-text-button">
                Search
              </button>
            </form>
            <div className="ops-customer-results" aria-label="Matching customers">
              {customers.map((c) => (
                <button
                  className="ops-lookup-row"
                  key={c.id}
                  onClick={() => setCustomer(c)}
                >
                  <span>
                    <strong>{c.name}</strong>
                    <small>{c.email}</small>
                  </span>
                  <ArrowRight size={16} />
                </button>
              ))}
              {!customers.length && !busy && (
                <p>
                  No matching customers. Customers must register before receiving
                  a license.
                </p>
              )}
            </div>
            <div className="ops-pagination">
              <button
                disabled={busy || skip === 0}
                onClick={() => search(skip - 50)}
              >
                Previous
              </button>
              <button
                disabled={busy || customers.length < 50}
                onClick={() => search(skip + 50)}
              >
                Next
              </button>
            </div>
          </div>
        ) : (
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              const form = new FormData(e.currentTarget);
              setBusy(true);
              setError("");
              const a = await client.api(
                new IssueLicense({
                  userId: customer.id,
                  licenseeName: String(form.get("name")),
                  licenseeOrganization:
                    String(form.get("organization")) || undefined,
                  seats: Number(form.get("seats")),
                  edition: Edition.Pro,
                  updateMode: lifetime
                    ? UpdateMode.Lifetime
                    : UpdateMode.ThroughDate,
                  updatesThroughUtc: lifetime
                    ? undefined
                    : new Date(
                        String(form.get("through")) + "T23:59:59Z",
                      ).toISOString(),
                }),
              );
              if (a.succeeded) {
                dialog.current?.close();
                onIssued();
              } else setError(a.error?.message ?? "Unable to issue license.");
              setBusy(false);
            }}
          >
            <button
              type="button"
              className="ops-text-button"
              onClick={() => setCustomer(undefined)}
            >
              <ArrowLeft size={13} /> {customer.email} · Change customer
            </button>
            <label>
              Registered name
              <input
                name="name"
                required
                defaultValue={customer.name}
                maxLength={200}
              />
            </label>
            <label>
              Organization (optional)
              <input name="organization" maxLength={200} />
            </label>
            <label>
              Seats
              <input
                name="seats"
                type="number"
                min={1}
                max={10000}
                required
                defaultValue={1}
              />
            </label>
            <label>
              Update coverage
              <select
                value={lifetime ? "lifetime" : "dated"}
                onChange={(e) => setLifetime(e.target.value === "lifetime")}
              >
                <option value="dated">Through a specific date</option>
                <option value="lifetime">Lifetime updates</option>
              </select>
            </label>
            {!lifetime && (
              <label>
                Updates through
                <input name="through" type="date" required />
              </label>
            )}
            <p>This grants Pro without charging the customer.</p>
            <button className="ops-button" disabled={busy}>
              {busy ? "Issuing…" : "Issue Pro license"}
            </button>
          </form>
        )}
      </dialog>
    </>
  );
}
