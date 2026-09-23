import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, expect, it } from 'vitest'
import ReleaseNotes from './release-notes'

afterEach(cleanup)

it('renders GitHub emphasis, bare links, and lists', () => {
  const url = 'https://github.com/NetCoreApps/acme-studio/commits/v1.0.0'
  const { container } = render(<ReleaseNotes>{`**Full Changelog**: ${url}\n\n- Windows build\n- Linux build`}</ReleaseNotes>)
  expect(container.querySelector('strong')?.textContent).toBe('Full Changelog')
  expect(screen.getByRole('link').getAttribute('href')).toBe(url)
  expect(screen.getAllByRole('listitem')).toHaveLength(2)
})

it('does not render raw HTML or executable Markdown links', () => {
  const { container } = render(<ReleaseNotes>{'<script>alert(1)</script>\n\n[unsafe](javascript:alert%281%29)'}</ReleaseNotes>)
  expect(container.querySelector('script')).toBeNull()
  expect(container.querySelector('a')?.getAttribute('href')).not.toMatch(/^javascript:/)
})
