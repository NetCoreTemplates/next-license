import Link from 'next/link'
import Layout from '@/components/layout'
import { PageHeader } from '@/components/page-parts'

export const metadata = {
  title: 'How licensing works',
  description: 'Using your license offline, installing newer builds, renewals and upgrades, optional refresh, and where to find receipts.',
}

export default function Docs() {
  return (
    <Layout>
      <div className="studio-page narrow">
        <PageHeader
          eyebrow="Documentation"
          title="Your license, explained"
          lead="Five things worth knowing about a perpetual, offline license — and none of them require an internet connection."
        />

        <div className="studio-prose" style={{ marginTop: 40 }}>
          <h2>Using your license offline</h2>
          <p>
            After purchase, open <Link href="/account">My licenses</Link> to download your signed license file.
            Keep it private: it contains your license key and readable licensee details. A paid license has no
            use-expiry and never needs a network connection to verify.
          </p>

          <h2>Installing a newer build</h2>
          <p>
            Downloads are public. A dated license unlocks Pro in builds released on or before its update cutoff.
            Newer builds need a renewal or lifetime coverage. Covered builds keep working forever, offline.
          </p>

          <h2>Renewals and upgrades</h2>
          <p>
            A 12-month renewal extends from the later of your current cutoff and the payment date, so renewing
            early keeps your remaining time. The renewal discount stays available for 60 days past the cutoff by
            default, and the portal shows the applicable price before checkout. A lifetime upgrade keeps your
            license identity and removes the dated cutoff.
          </p>

          <h2>Optional license refresh</h2>
          <p>
            Applications may offer a refresh that retrieves a renewed license without storing installation history.
            A failed refresh never blocks paid features. You can always download the updated license file from your account.
          </p>

          <h2>Orders and receipts</h2>
          <p>
            Your account lists every order and links to its Stripe invoice where one exists. Some payment methods
            settle later; your license appears once payment is confirmed.
          </p>
        </div>

        <p style={{ marginTop: 40, display: 'flex', gap: 20, flexWrap: 'wrap' }}>
          <Link className="text-link" href="/eula">License agreement</Link>
          <Link className="text-link" href="/privacy">Privacy</Link>
          <Link className="text-link" href="/changelog">Changelog</Link>
        </p>
      </div>
    </Layout>
  )
}
