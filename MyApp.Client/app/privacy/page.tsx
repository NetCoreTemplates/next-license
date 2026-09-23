import Layout from '@/components/layout'
import { PageHeader } from '@/components/page-parts'

export const metadata = {
  title: 'License privacy',
  description: 'What a license file reveals, what optional activation records, and how long records are kept.',
}

export default function Privacy() {
  return (
    <Layout>
      <div className="studio-page narrow">
        <PageHeader
          eyebrow="Privacy"
          title="What your license reveals"
          lead="License files are signed, not encrypted. Treat both the file and the short key as private bearer credentials."
        />

        <div className="studio-prose" style={{ marginTop: 40 }}>
          <h2>The license file</h2>
          <p>
            Anyone who receives your license file can read your name, organization, edition, seat count and update
            entitlement. Paid feature verification happens entirely offline, which also means revocation cannot
            remotely disable a file that has already been issued.
          </p>

          <h2>Optional license refresh</h2>
          <p>
            An application may request the latest license using its refresh key. The server does not store
            installation IDs, device details or activation history. Refresh is optional; paid features work offline.
          </p>

          <h2>Retention</h2>
          <p>
            Account, order, invoice and agreement records support your purchase and the
            operator&rsquo;s commercial recordkeeping.
          </p>

          <h2>Before you launch this template</h2>
          <p>
            This template requires the operator to publish a jurisdiction-specific retention and account-deletion
            policy before going live. Replace this page with yours.
          </p>
        </div>
      </div>
    </Layout>
  )
}
