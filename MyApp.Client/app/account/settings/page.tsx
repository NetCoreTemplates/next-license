'use client'
import { useEffect, useState } from 'react'
import Link from 'next/link'
import { ArrowLeft, ArrowUpRight } from 'lucide-react'
import { useClient } from '@servicestack/react'
import {
  GetAccountLicenses, SoftwareLicense,
  GetLicenseNotificationPreferences, SaveLicenseNotificationPreferences, LicenseNotificationPreferences,
  ListLicenseTransfers, LicenseTransfer, TransferLicense, AcceptLicenseTransfer, CancelLicenseTransfer,
} from '@/lib/dtos'
import Layout from '@/components/layout'
import { appAuth } from '@/lib/auth'
import { PageHeader, Alert, SkeletonCard } from '@/components/page-parts'

const emailOptions = [
  ['updateReminders', 'Dated update reminders', 'A nudge before your update cutoff passes.'],
  ['releaseAnnouncements', 'New release announcements', 'Every time a new build ships.'],
  ['winBack', 'New versions after my cutoff', 'Only when a release falls outside your coverage.'],
] as const

export default function AccountSettings() {
  // useClient returns a new state object on each render; mount effects must not depend on its identity.
  const client = useClient()
  const { user } = appAuth()
  const [licenses, setLicenses] = useState<SoftwareLicense[]>([])
  const [preferences, setPreferences] = useState<LicenseNotificationPreferences>()
  const [transfers, setTransfers] = useState<LicenseTransfer[]>([])
  const [licenseId, setLicenseId] = useState('')
  const [email, setEmail] = useState('')
  const [message, setMessage] = useState('')
  const [failed, setFailed] = useState(false)
  const [loading, setLoading] = useState(true)

  function report(ok: boolean, text: string) {
    setFailed(!ok)
    setMessage(text)
  }

  async function loadTransfers() {
    const api = await client.api(new ListLicenseTransfers())
    if (api.succeeded) setTransfers(api.response!.results ?? [])
  }

  useEffect(() => {
    void (async () => {
      const api = await client.api(new GetLicenseNotificationPreferences())
      if (api.succeeded) setPreferences(api.response)
      else report(false, api.error?.message ?? 'Sign in to manage your account.')
      await loadTransfers()
      const own = await client.api(new GetAccountLicenses())
      if (own.succeeded) setLicenses((own.response!.results ?? []).filter(x => x.status === 'Active'))
      setLoading(false)
    })()
  }, [])

  async function save() {
    const api = await client.apiVoid(new SaveLicenseNotificationPreferences(preferences))
    report(api.succeeded, api.succeeded ? 'Preferences saved.' : api.error?.message ?? 'Unable to save preferences.')
  }

  async function transfer() {
    const api = await client.api(new TransferLicense({ id: licenseId, recipientEmail: email }))
    report(api.succeeded, api.succeeded
      ? 'Transfer requested. The recipient must accept within seven days.'
      : api.error?.message ?? 'Unable to request transfer.')
    if (api.succeeded) await loadTransfers()
  }

  async function respond(id: string, accept: boolean) {
    const api = await client.apiVoid(accept ? new AcceptLicenseTransfer({ id }) : new CancelLicenseTransfer({ id }))
    report(api.succeeded, api.succeeded ? 'Transfer updated.' : api.error?.message ?? 'Unable to update transfer.')
    if (api.succeeded) await loadTransfers()
  }

  return (
    <Layout>
      <div className="studio-page narrow">
        <Link href="/account" className="text-link"><ArrowLeft size={15}/> Back to your licenses</Link>
        <div style={{ marginTop: 20 }}>
          <PageHeader
            eyebrow="Your account, your terms"
            title="Account settings"
            lead="Choose which emails you get, hand a license to someone else, and manage your personal data."
          />
        </div>

        {message && (
          <div style={{ marginTop: 24 }}>
            <Alert tone={failed ? 'error' : 'ok'} role={failed ? 'alert' : 'status'}>{message}</Alert>
          </div>
        )}

        {loading && <div style={{ marginTop: 28 }}><SkeletonCard rows={4}/></div>}

        {preferences && (
          <section className="studio-card" style={{ marginTop: 28 }}>
            <h2>Email preferences</h2>
            <p style={{ margin: '8px 0 18px', fontSize: 'var(--t-sm)', color: 'var(--ink-muted)' }}>
              Purchase and transfer emails are part of your account activity. Everything else is your choice.
            </p>
            {emailOptions.map(([key, label, help]) => (
              <label key={key} style={{ display: 'flex', gap: 12, alignItems: 'flex-start', padding: '12px 0', borderTop: '1px solid var(--line)' }}>
                <input type="checkbox" style={{ marginTop: 3 }} checked={!!preferences[key]}
                       onChange={e => setPreferences({ ...preferences, [key]: e.target.checked })}/>
                <span>
                  <span style={{ display: 'block', fontSize: 'var(--t-sm)', fontWeight: 600 }}>{label}</span>
                  <span style={{ display: 'block', fontSize: 'var(--t-xs)', color: 'var(--ink-subtle)' }}>{help}</span>
                </span>
              </label>
            ))}
            <button className="studio-button primary" style={{ marginTop: 18 }} onClick={save}>Save preferences</button>
          </section>
        )}

        {preferences && <>
          <section className="studio-card" style={{ marginTop: 18 }}>
            <h2>Transfer a license</h2>
            <p style={{ margin: '8px 0 18px', fontSize: 'var(--t-sm)', color: 'var(--ink-muted)', lineHeight: 1.8 }}>
              The recipient needs a verified account and must accept the transfer. This moves access to the license
              file, which still carries the existing licensee details. Orders and invoices stay with the buyer, and
              files that were already downloaded cannot be disabled remotely.
            </p>
            <label className="studio-field" style={{ marginBottom: 16 }}>
              <span>Your license</span>
              <select value={licenseId} onChange={e => setLicenseId(e.target.value)}>
                <option value="">Choose a license</option>
                {licenses.map(license => (
                  <option key={license.id} value={license.id}>
                    Acme Studio {license.edition} · {license.licenseeName} · ending {license.shortKeySuffix}
                  </option>
                ))}
              </select>
            </label>
            <label className="studio-field" style={{ marginBottom: 18 }}>
              <span>Recipient email</span>
              <input type="email" value={email} onChange={e => setEmail(e.target.value)} placeholder="name@example.com"/>
            </label>
            <button className="studio-button secondary" disabled={!licenseId || !email} onClick={transfer}>Request transfer</button>
          </section>

          <section style={{ marginTop: 36 }}>
            <div className="ops-section-title"><h2 style={{ fontSize: 'var(--t-lg)' }}>Pending transfers</h2></div>
            {transfers.length === 0 && (
              <p style={{ fontSize: 'var(--t-sm)', color: 'var(--ink-subtle)' }}>No pending transfers.</p>
            )}
            {transfers.map(t => (
              <div key={t.id} className="studio-card" style={{ marginTop: 12 }}>
                <p className="fact" style={{ overflowWrap: 'anywhere' }}>License {t.licenseId}</p>
                <p style={{ margin: '10px 0 16px', fontSize: 'var(--t-xs)', color: 'var(--ink-subtle)' }}>
                  Expires {t.expiresAtUtc ? new Date(t.expiresAtUtc).toLocaleDateString() : ''}.
                  Only the recipient may accept; only the sender may cancel.
                </p>
                <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
                  {t.toUserId === user?.userId && <button className="studio-button primary" onClick={() => respond(t.id!, true)}>Accept transfer</button>}
                  {t.fromUserId === user?.userId && <button className="studio-button secondary" onClick={() => respond(t.id!, false)}>Cancel transfer</button>}
                </div>
              </div>
            ))}
          </section>

          <section style={{ marginTop: 40, borderTop: '1px solid var(--line)', paddingTop: 24 }}>
            <h2 style={{ fontSize: 'var(--t-lg)' }}>Personal data</h2>
            <p style={{ margin: '10px 0 16px', fontSize: 'var(--t-sm)', color: 'var(--ink-muted)', lineHeight: 1.8 }}>
              Download your license files before deleting your account. Identity and activation data are removed;
              the minimum commercial evidence is kept.
            </p>
            <a className="text-link" href="/Identity/Account/Manage/PersonalData">
              Manage personal data <ArrowUpRight size={15}/>
            </a>
          </section>
        </>}
      </div>
    </Layout>
  )
}
