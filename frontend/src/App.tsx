import { Route, Routes } from 'react-router-dom'
import { AppShell } from './components/layout/AppShell'
import { DashboardPage } from './pages/DashboardPage'
import { FixturesPage } from './pages/FixturesPage'
import { MyTeamPage } from './pages/MyTeamPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { PlayersPage } from './pages/PlayersPage'
import { RecommendationsPage } from './pages/RecommendationsPage'
import { TransfersPage } from './pages/TransfersPage'

function App() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route index element={<DashboardPage />} />
        <Route path="team" element={<MyTeamPage />} />
        <Route path="transfers" element={<TransfersPage />} />
        <Route path="players" element={<PlayersPage />} />
        <Route path="fixtures" element={<FixturesPage />} />
        <Route path="recommendations" element={<RecommendationsPage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  )
}

export default App
