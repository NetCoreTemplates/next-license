'use client'
import { useEffect, useState } from 'react'
import { FileText } from 'lucide-react'
import { useClient } from '@servicestack/react'
import MarkdownContent from '@/components/markdown-content'
import { GetLicensePricing, LicenseAgreement } from '@/lib/dtos'
import Layout from '@/components/layout'
import { PageHeader, EmptyState, SkeletonCard, Alert } from '@/components/page-parts'

export default function Eula() {
  // useClient returns a new state object on each render; mount effects must not depend on its identity.
  const client = useClient()
  const [agreement, setAgreement] = useState<LicenseAgreement>()
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    void (async () => {
      const api = await client.api(new GetLicensePricing())
      if (api.succeeded) setAgreement(api.response!.agreement)
      else setError(api.error?.message ?? 'Unable to load the license agreement.')
      setLoading(false)
    })()
  }, [])

  return (
    <Layout>
      <div className="studio-page narrow">
        <PageHeader
          eyebrow="Legal"
          title="License agreement"
          lead={agreement
            ? <>The terms in force for new purchases. Version <span className="fact">{agreement.version}</span>.</>
            : 'The terms in force for new purchases.'}
        />
        <div style={{ marginTop: 40 }}>
          {loading && <SkeletonCard rows={6}/>}
          {error && <Alert tone="error" role="alert">{error}</Alert>}
          {!loading && !error && !agreement && (
            <EmptyState icon={<FileText size={26}/>} title="No agreement has been published">
              Purchases stay closed until the operator publishes license terms.
            </EmptyState>
          )}
          {agreement && <MarkdownContent>{agreement.bodyMarkdown}</MarkdownContent>}
        </div>
      </div>
    </Layout>
  )
}
