'use client'

import { SyntheticEvent, Suspense, useEffect, useState } from "react"
import { useClient, FormLoading, ErrorSummary, TextInput, PrimaryButton, ApiStateContext } from "@servicestack/react"
import { serializeToObject, leftPart, rightPart, toPascalCase } from "@servicestack/client"
import { useRouter, useSearchParams } from "next/navigation"
import Link from "next/link"
import Layout from "@/components/layout"
import { getRedirect } from "@/lib/gateway"
import { Register, RegisterResponse } from "@/lib/dtos"
import { appAuth, Redirecting } from "@/lib/auth"

function SignUpContent() {
    const client = useClient()
    const [displayName, setDisplayName] = useState<string>()
    const [username, setUsername] = useState<string>()
    const [password, setPassword] = useState<string>()
    const [confirmPassword, setConfirmPassword] = useState<string>()
    const router = useRouter()
    const searchParams = useSearchParams()
    const { user, revalidate } = appAuth()

    const setUser = (email: string) => {
        const first = leftPart(email, '@')
        const last = rightPart(leftPart(email, '.'), '@')
        setDisplayName(toPascalCase(first) + ' ' + toPascalCase(last))
        setUsername(email)
        setPassword('p@55wOrd')
        setConfirmPassword('p@55wOrd')
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

        const { displayName, userName, password, confirmPassword, autoLogin } = serializeToObject(e.currentTarget)
        if (password !== confirmPassword) {
            client.setError({ fieldName: 'confirmPassword', message: 'Passwords do not match' })
            return
        }

        const api = await client.api(new Register({ displayName, email: userName, password, confirmPassword, autoLogin }))
        if (api.succeeded) {
            await revalidate()
            const redirectUrl = (api.response as RegisterResponse).redirectUrl
            if (redirectUrl) {
                location.href = redirectUrl
            } else {
                router.push("/signin")
            }
        }
    }

    return (
        <div className="auth-page">
            <ApiStateContext.Provider value={client}>
                <section className="auth-card">
                    <p className="eyebrow">Create an account</p>
                    <h1>Keep your licenses in one place</h1>
                    <p>An account holds your license files, orders and account settings. The software itself never needs it.</p>
                    <form onSubmit={onSubmit}>
                        <ErrorSummary except="displayName,userName,password,confirmPassword"/>
                        <TextInput id="displayName" label="Name" help="Your first and last name" autoComplete="name"
                                   value={displayName} onChange={setDisplayName}/>
                        <TextInput id="userName" label="Email" autoComplete="email"
                                   value={username} onChange={setUsername}/>
                        <TextInput id="password" label="Password" type="password" help="6 characters or more"
                                   autoComplete="new-password"
                                   value={password} onChange={setPassword}/>
                        <TextInput id="confirmPassword" label="Confirm password" type="password"
                                   autoComplete="new-password"
                                   value={confirmPassword} onChange={setConfirmPassword}/>
                        {client.loading ? <FormLoading/> : null}
                        <PrimaryButton>Create account</PrimaryButton>
                    </form>
                    <p className="auth-footer">
                        Already have one? <Link className="text-link" href="/signin">Sign in</Link>
                    </p>
                </section>
            </ApiStateContext.Provider>

            <div className="auth-demo">
                <p>Demo account</p>
                <div>
                    <button type="button" onClick={() => setUser('new@user.com')}>new@user.com</button>
                </div>
            </div>
        </div>
    )
}

export default function SignUp() {
    return (
        <Layout>
            <Suspense fallback={<div className="auth-page"><div className="auth-card"><div className="skeleton" style={{height:300}}/></div></div>}>
                <SignUpContent />
            </Suspense>
        </Layout>
    )
}
