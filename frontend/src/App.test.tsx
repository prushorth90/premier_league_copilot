import { renderToStaticMarkup } from 'react-dom/server'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import App from './App'

function renderRoute(path: string) {
  return renderToStaticMarkup(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  )
}

describe('App routing', () => {
  it('renders the home page', () => {
    expect(renderRoute('/')).toContain('Make every transfer count.')
  })

  it('renders the not-found page for an unknown route', () => {
    expect(renderRoute('/unknown')).toContain('Page not found')
  })
})