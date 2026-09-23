import type { ReactNode } from 'react'

/** A page's opening block: label, headline, lead, and an optional action. */
export function PageHeader({ eyebrow, title, lead, action }: {
  eyebrow?: string
  title: ReactNode
  lead?: ReactNode
  action?: ReactNode
}) {
  return (
    <header className={action ? 'page-header-row' : undefined}>
      <div className="page-header">
        {eyebrow && <p className="eyebrow">{eyebrow}</p>}
        <h1>{title}</h1>
        {lead && <p>{lead}</p>}
      </div>
      {action}
    </header>
  )
}

export function Alert({ tone = 'info', role, children }: {
  tone?: 'info' | 'ok' | 'warn' | 'error'
  role?: 'alert' | 'status'
  children: ReactNode
}) {
  return (
    <div className={`studio-alert ${tone === 'info' ? '' : tone}`} role={role}>
      <span>{children}</span>
    </div>
  )
}

export function EmptyState({ icon, title, children, action }: {
  icon?: ReactNode
  title: string
  children?: ReactNode
  action?: ReactNode
}) {
  return (
    <div className="studio-empty">
      {icon}
      <h2>{title}</h2>
      {children && <p>{children}</p>}
      {action}
    </div>
  )
}

/** Placeholder with the shape of the content that is loading. */
export function SkeletonCard({ rows = 3 }: { rows?: number }) {
  return (
    <div className="skeleton-card" aria-hidden="true">
      <div className="skeleton" style={{ height: 20, width: '45%' }}/>
      {Array.from({ length: rows }, (_, i) => (
        <div key={i} className="skeleton" style={{ height: 12, width: `${90 - i * 12}%` }}/>
      ))}
    </div>
  )
}

/** Render a UTC instant the same way everywhere, in the mono voice. */
export function formatUtcDate(value?: string) {
  return value
    ? new Intl.DateTimeFormat('en', { dateStyle: 'long', timeZone: 'UTC' }).format(new Date(value))
    : '—'
}

/**
 * The update rule, drawn.
 *
 * This is a diagram of the entitlement, not a countdown: builds released up to
 * the cutoff are covered forever, later builds need a renewal. It never
 * compares against the current clock, so it cannot imply that a paid license
 * expires.
 */
export function CutoffRule({ updateMode, updatesThroughUtc }: {
  updateMode?: string
  updatesThroughUtc?: string
}) {
  const lifetime = updateMode === 'Lifetime'
  return (
    <div className="cutoff-rule">
      <div className={`cutoff-track ${lifetime ? 'lifetime' : ''}`} role="img"
           aria-label={lifetime
             ? 'Every build, now and in future, is covered'
             : `Builds released through ${formatUtcDate(updatesThroughUtc)} are covered; later builds need a renewal`}>
        <i/><b/>
      </div>
      <div className="cutoff-legend">
        {lifetime ? (
          <>
            <span><strong>Every build</strong> — now and in future</span>
            <span>No cutoff</span>
          </>
        ) : (
          <>
            <span><strong>Builds up to your cutoff</strong></span>
            <span>Later builds — renew</span>
          </>
        )}
      </div>
    </div>
  )
}
