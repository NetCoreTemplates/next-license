"use client"
import { useEffect, useState } from "react"
import Link from "next/link"
import { ScrollText } from "lucide-react"
import { useClient } from "@servicestack/react"
import ReleaseNotes from '@/components/release-notes'
import { GetGitHubDownloads, GitHubDownloadRelease } from "@/lib/dtos"
import Layout from "@/components/layout"
import { PageHeader, EmptyState, SkeletonCard, Alert, formatUtcDate } from "@/components/page-parts"

export default function Changelog() {
  // useClient returns a new state object on each render; mount effects must not depend on its identity.
  const client = useClient()
  const [releases, setReleases] = useState<GitHubDownloadRelease[]>()
  const [error, setError] = useState("")

  useEffect(() => {
    void (async () => {
      const api = await client.api(new GetGitHubDownloads())
      if (api.succeeded) setReleases(api.response!.results ?? [])
      else setError(api.error?.message ?? "Unable to load the changelog.")
    })()
  }, [])

  return (
    <Layout>
      <div className="studio-page narrow">
        <PageHeader
          eyebrow="Changelog"
          title="Every build, on the record"
          lead="Your license covers Pro in versions released on or before its update cutoff. The release date below is the one that matters."
          action={<Link href="/download" className="studio-button secondary">Download a build</Link>}
        />

        <div className="changelog-list">
          {error && <Alert tone="error" role="alert">{error}</Alert>}
          {!releases && !error && <><SkeletonCard rows={3}/><SkeletonCard rows={2}/></>}
          {releases?.length === 0 && (
            <EmptyState icon={<ScrollText size={26}/>} title="No releases published yet">
              Release notes appear here as soon as the first build ships.
            </EmptyState>
          )}
          {releases?.map(release => (
            <section key={release.tag} className="studio-card changelog-entry">
              <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
                <h2 style={{ fontSize: 'var(--t-lg)' }}>{release.name || release.tag}</h2>
                <span className={`studio-pill ${release.prerelease ? 'amber' : 'green'}`}>
                  {release.prerelease ? 'Preview' : 'Stable'}
                </span>
              </div>
              <p className="fact" style={{ marginTop: 8, color: 'var(--ink-subtle)' }}>
                {(release.name && release.name !== release.tag) && <>{release.tag} · </>}released {formatUtcDate(release.publishedAt)}
              </p>
              <ReleaseNotes>{release.notes}</ReleaseNotes>
            </section>
          ))}
        </div>
      </div>
    </Layout>
  )
}
