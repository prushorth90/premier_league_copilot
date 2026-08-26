import { Link, Route, Routes } from 'react-router-dom'

function HomePage() {
  return (
    <main className="mx-auto flex w-full max-w-6xl flex-1 items-center px-6 py-16 sm:px-10">
      <section className="max-w-3xl">
        <p className="mb-4 text-sm font-bold uppercase text-emerald-700">
          Fantasy Premier League
        </p>
        <h1 className="font-display text-5xl leading-tight font-semibold text-zinc-950 sm:text-7xl">
          Make every transfer count.
        </h1>
      </section>
    </main>
  )
}

function NotFoundPage() {
  return (
    <main className="grid flex-1 place-items-center px-6 text-center">
      <div>
        <p className="text-sm font-bold text-emerald-700">404</p>
        <h1 className="font-display mt-3 text-4xl font-semibold text-zinc-950">
          Page not found
        </h1>
        <Link className="mt-6 inline-block font-semibold text-emerald-700" to="/">
          Return home
        </Link>
      </div>
    </main>
  )
}

function App() {
  return (
    <div className="flex min-h-screen flex-col bg-stone-50">
      <header className="border-b border-zinc-200 bg-white">
        <nav className="mx-auto flex h-16 w-full max-w-6xl items-center px-6 sm:px-10">
          <Link className="font-display text-xl font-semibold text-zinc-950" to="/">
            Touchline
          </Link>
        </nav>
      </header>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </div>
  )
}

export default App
