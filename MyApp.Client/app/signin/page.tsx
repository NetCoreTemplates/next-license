'use client'

import { SyntheticEvent, Suspense, useEffect, useState } from "react"
import { useRouter, useSearchParams } from "next/navigation"
import Link from "next/link"
import { ErrorSummary, TextInput, PrimaryButton, useClient, ApiStateContext } from "@servicestack/react"

import Layout from "@/components/layout"
import { Authenticate } from "@/lib/dtos"
import { appAuth, Redirecting } from "@/lib/auth"
import { getRedirect } from "@/lib/gateway"

const demoAccounts = process.env.NODE_ENV === 'development'
    ? ['admin@email.com', 'manager@email.com', 'employee@email.com', 'new@user.com']
    : []

function SignInContent() {
    const client = useClient()
    const [userName, setUserName] = useState<string|undefined>()
    const [password, setPassword] = useState<string|undefined>()
    const router = useRouter()
    const searchParams = useSearchParams()
    const { user, revalidate } = appAuth()

    const setUser = (email: string) => {
        setUserName(email)
        setPassword('p@55wOrd')
    }

    useEffect(() => {
        if (user) {
            const redirect = getRedirect(Object.fromEntries(searchParams.entries())) || "/"
            router.replace(redirect)
        }
    }, [user])
    if (user) return <Redirecting/>

    const onSubmit = async (e: SyntheticEvent<HTMLFormElement>) => {
        e.preventDefault()
        const api = await client.api(new Authenticate({ provider:'credentials', userName, password }))
        if (api.succeeded)
            await revalidate()
    }

    return (
        <div className="auth-page">
            <ApiStateContext.Provider value={client}>
                <section className="auth-card">
                    <p className="eyebrow">Your licenses</p>
                    <h1>Welcome back</h1>
                    <p>Sign in to download your license files, check orders, and manage your account.</p>
                    <form onSubmit={onSubmit}>
                        <ErrorSummary except="userName,password"/>
                        <TextInput id="userName" label="Email" help="Email you signed up with" autoComplete="email"
                                   value={userName} onChange={setUserName}/>
                        <TextInput id="password" label="Password" type="password" help="6 characters or more"
                                   autoComplete="current-password"
                                   value={password} onChange={setPassword}/>
                        <PrimaryButton>Sign in</PrimaryButton>
                    </form>
                    <p className="auth-footer">
                        New here? <Link className="text-link" href="/signup">Create an account</Link>
                    </p>
                </section>
            </ApiStateContext.Provider>

            {demoAccounts.length > 0 && <div className="auth-demo">
                <p>Demo accounts</p>
                <div>
                    {demoAccounts.map(email => (
                        <button key={email} type="button" onClick={() => setUser(email)}>{email}</button>
                    ))}
                </div>
            </div>}
        </div>
    )
}

export default function SignIn() {
    return (
        <Layout>
            <Suspense fallback={<div className="auth-page"><div className="auth-card"><div className="skeleton" style={{height:220}}/></div></div>}>
                <SignInContent />
            </Suspense>
        </Layout>
    )
}
