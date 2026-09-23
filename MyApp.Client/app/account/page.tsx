'use client'
import { useEffect, useState } from 'react'
import Link from 'next/link'
import { ArrowRight, Check, Copy, Download, KeyRound, LockKeyhole, Mail, Receipt, Settings2 } from 'lucide-react'
import { useClient } from '@servicestack/react'
import MarkdownContent from '@/components/markdown-content'
import {
  GetAccountLicenses, GetAccountOrders, RefreshAccountOrder, GetLicenseBlob, GetOrderInvoice,
  ResendLicense, GetLicensePricing, CreateLicenseCheckout,
  SoftwareLicense, CustomerOrder, LicensePricingResponse,
} from '@/lib/dtos'
import Layout from '@/components/layout'
import { PageHeader, EmptyState, SkeletonCard, Alert, CutoffRule, formatUtcDate } from '@/components/page-parts'

const orderLabel = (order: CustomerOrder) =>
  order.status === 'Pending' ? (order.requiresReview ? 'Delivery needs attention' : 'Confirming payment')
  : order.status === 'Paid' ? 'Paid'
  : order.status === 'Failed' ? 'Not completed'
  : order.status === 'PartiallyRefunded' ? 'Partially refunded'
  : 'Refunded'

const orderTone = (order: CustomerOrder) =>
  order.status === 'Paid' ? 'green' : order.status === 'Pending' ? 'amber' : 'slate'

export default function Licenses() {
  // useClient returns a new state object on each render; mount effects must not depend on its identity.
  const client = useClient()
  const [licenses, setLicenses] = useState<SoftwareLicense[]>()
  const [orders, setOrders] = useState<CustomerOrder[]>()
  const [pricing, setPricing] = useState<LicensePricingResponse>()
  const [purchase, setPurchase] = useState<{ license: SoftwareLicense; sku: string }>()
  const [accepted, setAccepted] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [copied, setCopied] = useState<string>()
  const [checkingOrder, setCheckingOrder] = useState<string>()
  const [signedOut, setSignedOut] = useState(false)

  useEffect(() => {
    void (async () => {
      const [licenseApi, orderApi, priceApi] = await Promise.all([
        client.api(new GetAccountLicenses()), client.api(new GetAccountOrders()), client.api(new GetLicensePricing()),
      ])
      // Not being signed in is a next step, not a failure.
      if (licenseApi.error?.errorCode === 'Unauthorized') { setSignedOut(true); return }
      if (licenseApi.succeeded) setLicenses(licenseApi.response!.results ?? [])
      else setError(licenseApi.error?.message ?? 'Sign in to view your licenses.')
      if (orderApi.succeeded) {
        const current = orderApi.response!.results ?? []
        setOrders(current)
        if (new URLSearchParams(window.location.search).get('checkout') === 'complete') {
          for (const order of current.filter(o => o.status === 'Pending').slice(0, 5)) await refreshOrder(order.id!)
        }
      } else setError(orderApi.error?.message ?? 'Unable to load your orders.')
      if (priceApi.succeeded) setPricing(priceApi.response)
    })()
  }, [])

  async function refreshOrder(id: string) {
    setCheckingOrder(id); setError('')
    const api = await client.api(new RefreshAccountOrder({ id }))
    if (api.succeeded && api.response) {
      const updated = api.response
      setOrders(current => current?.map(order => order.id === id ? updated : order))
      setNotice(updated.message ?? 'Payment status updated.')
      if (updated.status === 'Paid') {
        const licensesApi = await client.api(new GetAccountLicenses())
        if (licensesApi.succeeded) setLicenses(licensesApi.response?.results ?? [])
        else setError(licensesApi.error?.message ?? 'Payment confirmed. Reload to see your license.')
      }
    } else setError(api.error?.message ?? 'Unable to check payment status. Try again.')
    setCheckingOrder(undefined)
  }

  async function download(id: string) {
    const api = await client.api(new GetLicenseBlob({ id }))
    if (!api.succeeded || !api.response!.blob) {
      setError(api.error?.message ?? 'This license is unavailable for refresh.')
      return
    }
    const url = URL.createObjectURL(new Blob([api.response!.blob], { type: 'text/plain' }))
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = `acme-${id}.license`
    anchor.click()
    setTimeout(() => URL.revokeObjectURL(url), 1000)
  }

  async function copyKey(id: string) {
    const api = await client.api(new GetLicenseBlob({ id }))
    if (!api.succeeded || !api.response?.blob) { setError(api.error?.message ?? 'License unavailable.'); return }
    try {
      await navigator.clipboard.writeText(api.response.blob)
      setCopied(id)
      setNotice('License key copied. Paste it into your app.')
      setTimeout(() => setCopied(current => current === id ? undefined : current), 2500)
    } catch { setError('Clipboard unavailable. Download the license file instead.') }
  }

  async function resend(id: string) {
    const api = await client.apiVoid(new ResendLicense({ id }))
    if (api.succeeded) { setError(''); setNotice('A delivery email has been queued for your verified email address.') }
    else setError(api.error?.message ?? 'Unable to resend.')
  }

  async function invoice(id: string) {
    const api = await client.api(new GetOrderInvoice({ id }))
    if (api.succeeded && api.response!.url) window.location.assign(api.response!.url)
    else setError(api.error?.message ?? 'Invoice not yet available.')
  }

  async function checkout() {
    if (!purchase) return
    setBusy(true)
    const api = await client.api(new CreateLicenseCheckout({
      sku: purchase.sku, licenseId: purchase.license.id, seats: purchase.license.seats,
      licenseeName: purchase.license.licenseeName, acceptAgreement: accepted,
      agreementVersion: pricing?.agreement?.version,
    }))
    setBusy(false)
    if (api.succeeded && api.response!.url) window.location.assign(api.response!.url)
    else setError(api.error?.message ?? 'Unable to start checkout.')
  }

  function selectPurchase(license: SoftwareLicense, sku: string) {
    setAccepted(false)
    setPurchase({ license, sku })
  }

  return <Layout>
    <div className="studio-page wide">
      <PageHeader
        eyebrow="Your workspace, for the long run"
        title="Your licenses"
        lead="Your features stay yours. Manage license files, updates and purchases in one place."
        action={!signedOut && <Link href="/account/settings" className="studio-button secondary"><Settings2 size={16}/> Account settings</Link>}
      />

      <div style={{ display: 'grid', gap: 12, marginTop: 28 }}>
        {notice && <Alert tone="ok" role="status">{notice}</Alert>}
        {error && <Alert tone="error" role="alert">{error}</Alert>}
      </div>

      {signedOut && (
        <div style={{ marginTop: 20 }}>
          <EmptyState
            icon={<LockKeyhole size={26}/>}
            title="Sign in to see your licenses"
            action={<Link href="/signin?redirect=/account" className="studio-button primary">Sign in <ArrowRight size={16}/></Link>}
          >
            License files, update coverage and orders are kept with your account.
            New here? <Link className="text-link" href="/signup">Create an account</Link>.
          </EmptyState>
        </div>
      )}

      {!licenses && !error && !signedOut && <div style={{ marginTop: 20 }}><SkeletonCard rows={4}/></div>}

      {licenses?.length === 0 && (
        <div style={{ marginTop: 20 }}>
          <EmptyState
            icon={<KeyRound size={26}/>}
            title="No licenses yet"
            action={<Link href="/pricing" className="studio-button primary">See license options</Link>}
          >
            If you have just paid, use “Check payment status” on your order below to retrieve your license.
          </EmptyState>
        </div>
      )}

      {licenses?.map(license => {
        const dated = license.updateMode === 'ThroughDate'
        const revoked = license.status === 'Revoked'
        return (
          <article className="license-card" key={license.id}>
            <div className="license-card-head">
              <div>
                <h2>Acme Studio {license.edition}</h2>
                <p>{license.licenseeName}{license.licenseeOrganization ? ` · ${license.licenseeOrganization}` : ''}</p>
              </div>
              <span className={`studio-pill ${revoked ? 'red' : 'green'}`}>{revoked ? 'Refresh revoked' : 'Active'}</span>
            </div>

            <dl className="license-facts">
              <div><dt>Seats</dt><dd>{license.seats}</dd></div>
              <div><dt>Key ending</dt><dd>{license.shortKeySuffix}</dd></div>
              <div><dt>Updates</dt><dd>{license.updateMode === 'Lifetime' ? 'Lifetime' : 'Dated'}</dd></div>
              <div><dt>Verification</dt><dd>Offline</dd></div>
            </dl>

            <CutoffRule updateMode={license.updateMode} updatesThroughUtc={license.updatesThroughUtc}/>

            <p style={{ marginTop: 14, fontSize: 'var(--t-sm)', color: 'var(--ink-muted)' }} title={license.updatesThroughUtc}>
              {license.updateMode === 'Lifetime' ? 'Includes every future Pro version'
                : `Covers app versions released through ${formatUtcDate(license.updatesThroughUtc)} (UTC)`}
            </p>
            <p style={{ marginTop: 6, fontSize: 'var(--t-sm)', color: 'var(--ink-subtle)' }}>
              Your paid features keep working offline.
            </p>
            {revoked && (
              <p style={{ marginTop: 8, fontSize: 'var(--t-sm)', color: 'var(--ink-subtle)' }}>
                Refresh has been revoked. Previously issued offline files remain usable.
              </p>
            )}

            <div className="license-actions">
              <button className="studio-button primary" disabled={revoked} onClick={() => copyKey(license.id!)}>
                {copied === license.id ? <><Check size={16}/> Copied</> : <><Copy size={16}/> Copy license key</>}
              </button>
              <button className="studio-button secondary" disabled={revoked} onClick={() => download(license.id!)}>
                <Download size={16}/> Download license file
              </button>
              <button className="studio-button ghost" onClick={() => resend(license.id!)}>
                <Mail size={16}/> Resend delivery email
              </button>
              {license.status === 'Active' && dated && pricing?.agreement && <>
                {pricing.results?.some(p => p.sku === 'pro-12m-renewal') &&
                  <button className="studio-button ghost" onClick={() => selectPurchase(license, 'pro-12m-renewal')}>Renew version updates</button>}
                {pricing.results?.some(p => p.sku === 'pro-lifetime-upgrade') &&
                  <button className="studio-button ghost" onClick={() => selectPurchase(license, 'pro-lifetime-upgrade')}>Upgrade to lifetime updates</button>}
              </>}
            </div>

          </article>
        )
      })}

      {purchase && (
        <section aria-label="Confirm update purchase" className="studio-card" style={{ marginTop: 24, display: 'grid', gap: 16 }}>
          <h2 style={{ fontSize: 'var(--t-lg)' }}>
            {purchase.sku === 'pro-lifetime-upgrade' ? 'Lifetime version updates' : 'Renew version updates'}
          </h2>
          <p style={{ fontSize: 'var(--t-sm)', color: 'var(--ink-muted)' }}>
            Your covered app versions remain yours either way. Stripe shows the final price before you pay.
            Renewals outside the discount eligibility period use the new-purchase price.
          </p>
          <details>
            <summary>License agreement · <span className="fact">{pricing?.agreement?.version}</span></summary>
            <MarkdownContent>{pricing?.agreement?.bodyMarkdown}</MarkdownContent>
          </details>
          <label style={{ display: 'flex', gap: 10, alignItems: 'center', fontSize: 'var(--t-sm)' }}>
            <input type="checkbox" checked={accepted} onChange={event => setAccepted(event.target.checked)}/>
            I accept this license agreement.
          </label>
          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
            <button className="studio-button primary" disabled={!accepted || busy} onClick={checkout}>
              {busy ? 'Opening checkout…' : 'Continue to checkout'}
            </button>
            <button className="studio-button ghost" onClick={() => setPurchase(undefined)}>Cancel</button>
          </div>
        </section>
      )}

      {orders && (
        <section aria-label="Orders" style={{ marginTop: 48 }}>
          <div className="ops-section-title">
            <h2 style={{ fontSize: 'var(--t-lg)' }}>Orders</h2>
          </div>
          {orders.length === 0 && (
            <EmptyState icon={<Receipt size={26}/>} title="No orders yet">
              Your purchases and invoices will appear here.
            </EmptyState>
          )}
          {orders.map(order => (
            <article className="account-order" key={order.id}>
              <div className="account-order-heading">
                <div>
                  <h3>{order.productName}</h3>
                  <p>{order.description} · {order.seats} seat{order.seats === 1 ? '' : 's'}</p>
                </div>
                <strong>{new Intl.NumberFormat('en', { style: 'currency', currency: order.currency ?? 'usd' }).format((order.amountCents ?? 0) / 100)}</strong>
              </div>
              <div className="account-order-meta">
                <span className={`studio-pill ${orderTone(order)}`}>{orderLabel(order)}</span>
                <span>{order.createdAtUtc && new Date(order.createdAtUtc).toLocaleDateString(undefined, { dateStyle: 'medium' })}</span>
              </div>
              {order.message && <p className="account-order-message">{order.message}</p>}
              <div style={{ marginTop: 16, display: 'flex', flexWrap: 'wrap', gap: 10 }}>
                {order.status === 'Pending' && (
                  <button className="studio-button secondary" disabled={!!checkingOrder} onClick={() => refreshOrder(order.id!)}>
                    {checkingOrder === order.id ? 'Checking Stripe…' : 'Check payment status'}
                  </button>
                )}
                {order.hasInvoice && (
                  <button className="studio-button ghost" onClick={() => invoice(order.id!)}><Receipt size={16}/> View invoice</button>
                )}
              </div>
            </article>
          ))}
        </section>
      )}
    </div>
  </Layout>
}
