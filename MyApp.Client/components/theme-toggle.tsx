'use client'
import { useEffect, useState } from 'react'
import { Moon, Sun } from 'lucide-react'

/**
 * Light/dark switch sharing the `color-scheme` key read by the pre-paint
 * script in app/layout.tsx. The theme is only known in the browser, so the
 * icon renders after mount to keep server and client markup identical.
 */
export default function ThemeToggle() {
  const [dark, setDark] = useState<boolean>()

  useEffect(() => setDark(document.documentElement.classList.contains('dark')), [])

  function toggle() {
    const next = !document.documentElement.classList.contains('dark')
    document.documentElement.classList.toggle('dark', next)
    try { localStorage.setItem('color-scheme', next ? 'dark' : 'light') } catch {}
    setDark(next)
  }

  const label = dark ? 'Switch to light theme' : 'Switch to dark theme'
  return (
    <button type="button" className="theme-toggle" onClick={toggle} aria-label={label} title={label}>
      {dark === undefined ? null : dark ? <Sun size={17}/> : <Moon size={17}/>}
    </button>
  )
}
