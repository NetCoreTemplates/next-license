import { render, screen, cleanup } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { ReactNode } from 'react'
import Licenses from './page'
const mocks = vi.hoisted(() => ({ api: vi.fn() }))
vi.mock('@servicestack/react', () => ({ useClient: () => ({ ...mocks }) }))
vi.mock('@/components/layout', () => ({ default: ({ children }: { children: ReactNode }) => <main>{children}</main> }))
afterEach(() => { cleanup(); mocks.api.mockReset() })
describe('perpetual license status', () => {
  it('explains dated and lifetime entitlements without describing paid licenses as expired', async () => {
    mocks.api.mockImplementation(async (request: { getTypeName(): string }) => ({ succeeded: true, response: {
      results: request.getTypeName() === 'GetAccountLicenses' ? [
        { id: 'dated', edition: 'Pro', updateMode: 'ThroughDate', updatesThroughUtc: '2020-09-20T00:00:00Z', status: 'Active', seats: 1 },
        { id: 'lifetime', edition: 'Pro', updateMode: 'Lifetime', status: 'Active', seats: 1 },
      ] : [],
    } }))
    render(<Licenses/>)
    expect(await screen.findByText('Covers app versions released through September 20, 2020 (UTC)')).toBeTruthy()
    expect(screen.getByText('Includes every future Pro version')).toBeTruthy()
    expect(screen.queryByText(/expired/i)).toBeNull()
    expect(mocks.api).toHaveBeenCalledTimes(3)
  })
  it('displays authorization failures instead of an empty license list', async () => {
    mocks.api.mockResolvedValue({ succeeded: false, error: { message: 'Sign in to view your licenses.' } })
    render(<Licenses/>)
    expect((await screen.findByRole('alert')).textContent).toBe('Sign in to view your licenses.')
    expect(screen.queryByText(/No licenses yet/)).toBeNull()
  })
})

it('shows product and price instead of an opaque order reference', async () => {
  mocks.api.mockImplementation(async (request: { getTypeName(): string }) => ({ succeeded: true, response: {
    results: request.getTypeName() === 'GetAccountOrders' ? [{ id: 'order-1', productName: 'Acme Studio Pro', description: '12 months of updates', amountCents: 4900, currency: 'usd', seats: 1, status: 'Pending', requiresReview: true, message: 'License delivery needs attention.', orderNumber: 'ACME-opaque-reference' }] : [],
  } }))
  render(<Licenses/>)
  expect(await screen.findByText('Acme Studio Pro')).toBeTruthy()
  expect(screen.getByText('$49.00')).toBeTruthy()
  expect(screen.getByText('Delivery needs attention')).toBeTruthy()
  expect(screen.getByRole('button', { name: 'Check payment status' })).toBeTruthy()
  expect(screen.queryByText(/ACME-opaque-reference/)).toBeNull()
})

it('invites signed-out visitors to sign in instead of showing an error', async () => {
  mocks.api.mockResolvedValue({ succeeded: false, error: { errorCode: 'Unauthorized', message: 'Sign in to view your account.' } })
  render(<Licenses/>)
  expect(await screen.findByRole('heading', { name: 'Sign in to see your licenses' })).toBeTruthy()
  expect(screen.getByRole('link', { name: /Sign in/ }).getAttribute('href')).toBe('/signin?redirect=/account')
  expect(screen.queryByRole('alert')).toBeNull()
})
