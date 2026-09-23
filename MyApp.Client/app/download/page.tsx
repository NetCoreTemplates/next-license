"use client"
import { useEffect, useState } from "react"
import Link from "next/link"
import { Apple, Download as DownloadIcon, Github, Monitor, Terminal } from "lucide-react"
import { client } from "@/lib/gateway"
import { GetGitHubDownloads, GitHubDownloadsResponse } from "@/lib/dtos"
import { detectPlatform, downloadableBuilds, packageLabel, platformLabel } from "@/lib/software-downloads"
import ReleaseNotes from "@/components/release-notes"
import Layout from "@/components/layout"
import { PageHeader, EmptyState, SkeletonCard, Alert, formatUtcDate } from "@/components/page-parts"

const platformIcon = (name?: string) => {
  const label = platformLabel(name)
  return label === 'macOS' ? <Apple size={16}/> : label === 'Linux' ? <Terminal size={16}/> : label === 'Windows' ? <Monitor size={16}/> : <DownloadIcon size={16}/>
}

const megabytes = (bytes = 0) => `${(bytes / 1048576).toFixed(1)} MB`

export default function Download() {
  const [data, setData] = useState<GitHubDownloadsResponse>()
  const [error, setError] = useState("")
  // Known only in the browser; resolved after mount so markup stays stable.
  const [platform, setPlatform] = useState<string>()
  useEffect(() => setPlatform(detectPlatform(navigator.userAgent)), [])
  const latestStable = data?.results?.find(r => !r.prerelease)?.tag

  async function load() {
    setError("")
    setData(undefined)
    const api = await client.api(new GetGitHubDownloads())
    if (api.succeeded) setData(api.response)
    else setError(api.error?.message ?? "Unable to load releases.")
  }

  useEffect(() => { void load() }, [])

  return (
    <Layout>
      <div className="studio-page wide">
        <PageHeader
          eyebrow="Made to be yours"
          title={<>Your next great idea<br/>starts with a download.</>}
          lead="Get the latest build for your platform. Start free, then unlock Pro with a perpetual license — your covered versions keep working offline."
          action={<Link href="/pricing" className="studio-button secondary">See license options</Link>}
        />

        <div style={{ marginTop: 40, display: 'grid', gap: 18 }}>
          {error && (
            <Alert tone="error" role="alert">
              {error}{' '}
              <button onClick={() => void load()} className="ops-text-button">Try again</button>
            </Alert>
          )}
          {!data && !error && <><SkeletonCard rows={4}/><SkeletonCard rows={3}/></>}
          {data?.results?.length === 0 && (
            <EmptyState icon={<Github size={26}/>} title="Something good is on its way">
              Downloads appear here as soon as the first release is published.
            </EmptyState>
          )}
          {data?.results?.map(release => {
            const builds = downloadableBuilds(release.assets)
            const title = release.name || release.tag
            // Point people at their own platform's build of the newest stable release.
            const recommend = release.tag === latestStable && platform
              ? builds.find(a => platformLabel(a.name) === platform)
              : undefined
            return (
              <article className="studio-card" key={release.tag}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 14, flexWrap: 'wrap' }}>
                  <span className="ops-integration-icon"><DownloadIcon size={20}/></span>
                  <div style={{ flex: '1 1 220px', minWidth: 0 }}>
                    <h2 style={{ fontSize: 'var(--t-lg)' }}>{title}</h2>
                    <p className="fact" style={{ marginTop: 6, color: 'var(--ink-subtle)' }}>
                      {title !== release.tag && <>{release.tag} · </>}released {formatUtcDate(release.publishedAt)}
                    </p>
                  </div>
                  <span className={`studio-pill ${release.prerelease ? 'amber' : 'green'}`}>
                    {release.prerelease ? 'Preview' : release.tag === latestStable ? 'Latest stable' : 'Stable'}
                  </span>
                </div>

                <ReleaseNotes>{release.notes}</ReleaseNotes>

                {builds.length > 0 ? (
                  <div className="download-assets">
                    {builds.map(asset => (
                      <a className={`download-asset ${asset === recommend ? 'recommended' : ''}`} href={asset.url} key={asset.url}>
                        <span className="download-asset-icon">{platformIcon(asset.name)}</span>
                        <span className="download-asset-text">
                          <strong>
                            {platformLabel(asset.name)}
                            <span>{packageLabel(asset.name)}</span>
                          </strong>
                          <small className="fact">{megabytes(asset.size ?? 0)} · {asset.name}</small>
                        </span>
                        {asset === recommend && <span className="download-asset-tag">Recommended for {platform}</span>}
                        <DownloadIcon size={16} className="download-asset-arrow"/>
                      </a>
                    ))}
                  </div>
                ) : (
                  <p style={{ marginTop: 18, fontSize: 'var(--t-sm)', color: 'var(--ink-subtle)' }}>
                    No platform builds have been attached to this release yet.
                  </p>
                )}
              </article>
            )
          })}
        </div>

        <p style={{ marginTop: 32 }}>
          <Link className="text-link" href="/changelog">Read the full changelog</Link>
        </p>
      </div>
    </Layout>
  )
}
