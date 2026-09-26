import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { AppShell } from '@astryxdesign/core/AppShell'
import { TopNav, TopNavHeading } from '@astryxdesign/core/TopNav'
import { SideNav, SideNavItem } from '@astryxdesign/core/SideNav'
import { Collapsible, CollapsibleGroup } from '@astryxdesign/core/Collapsible'
import { MobileNav } from '@astryxdesign/core/MobileNav'
import { Icon } from '@astryxdesign/core/Icon'
import { Layout as Panes, LayoutContent } from '@astryxdesign/core/Layout'
import { Center } from '@astryxdesign/core/Center'
import { useMediaQuery } from '@astryxdesign/core/hooks'
import { Outline } from '@astryxdesign/core/Outline'
import { Button } from '@astryxdesign/core/Button'
import { IconButton } from '@astryxdesign/core/IconButton'
import { Kbd } from '@astryxdesign/core/Kbd'
import { HStack, VStack } from '@astryxdesign/core/Stack'
import { Text } from '@astryxdesign/core/Text'
import { GROUPS } from './content'
import type { Heading } from './content'
import { route } from './links'
import { navigate } from './router'
import { ScrollRootProvider } from './scrollRoot'
import { Search } from './Search'
import { ContrastIcon } from './ContrastIcon'

/**
 * The shell: a bar, the reading order, the page, and where you are in it.
 *
 * Responsive contract, at the frame root:
 *   > 1200px  nav 260 | page (centred, up to 1180) | outline 300
 *   <= 1200px the outline drops rather than squeezing the page
 *   <= 768px  the side nav collapses into AppShell's mobile drawer
 *   `fills`   no side nav at any width — the reading order is a drawer the
 *             bar's burger opens, at every width rather than below one
 *
 * Two shapes, one shell. A page of the book scrolls between the nav and its own
 * contents; the workbench is an application that owns the viewport and cannot
 * share it with an outline. `height` is what separates them — `auto` lets a page
 * grow, `fill` hands the viewport to the panes inside it.
 */
/** The group a page sits in, which is the section the reading order opens at. */
function groupOf(slug: string): string {
  return GROUPS.find((group) => group.pages.some((page) => page.slug === slug))?.label ?? ''
}

export function Layout({
  slug,
  toc,
  fills,
  onToggleMode,
  children,
}: {
  slug: string
  toc: Heading[]
  /** The page is an application: it takes the height and does its own scrolling. */
  fills?: boolean
  onToggleMode: () => void
  children: ReactNode
}) {
  const [searching, setSearching] = useState(false)
  const [navOpen, setNavOpen] = useState(false)

  // The sections standing open. The page's own group is always one of them; the
  // rest are whatever the reader has unfolded.
  const [openGroups, setOpenGroups] = useState<string[]>(() => [groupOf(slug)])
  useEffect(() => {
    const group = groupOf(slug)
    setOpenGroups((open) => (open.includes(group) ? open : [...open, group]))
  }, [slug])
  // The outline is the first thing to go: below this the three regions cannot
  // all have their width, and the one a reader can do without is the one that
  // only says where they are.
  const roomForTheOutline = useMediaQuery('(min-width: 1200px)')
  // A phone in portrait, where the bar is the one row that cannot scroll: the
  // tagline and the keyboard hint together took it past the screen's width, and
  // a bar wider than the screen zooms the whole document out — every page, not
  // just this one. Both are what a phone can most afford to lose: a line the
  // reader has already read, and a shortcut a touch screen cannot press.
  const phone = useMediaQuery('(max-width: 640px)')

  // `/` and ⌘K open search from anywhere that is not already taking the key.
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement | null
      const typing =
        target instanceof HTMLInputElement ||
        target instanceof HTMLTextAreaElement ||
        target?.isContentEditable
      if (typing) return
      if (event.key === '/' || ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k')) {
        event.preventDefault()
        setSearching(true)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  // Most links in the prose are rendered by the markdown renderer rather than
  // written here: one listener at the top keeps every one of them a navigation
  // rather than a page load.
  useEffect(() => {
    const onClick = (event: MouseEvent) => {
      if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey) return
      const link = (event.target as HTMLElement | null)?.closest('a')
      if (!link) return
      const href = link.getAttribute('href')
      if (!href || link.target === '_blank') return
      if (/^(https?:|mailto:)/.test(href)) return
      event.preventDefault()
      // A link in the drawer is a navigation, and the drawer has to get out of
      // the way of the page it just asked for. Closed from the click rather than
      // from an effect watching the location, because the location is not always
      // what moves: a name already showing changes nothing to react to, and the
      // drawer would sit there over the page it had just been asked to leave.
      setNavOpen(false)
      navigate(href)
    }
    // Back and forward move the page with no click to hang that off.
    const onPop = () => setNavOpen(false)
    document.addEventListener('click', onClick)
    window.addEventListener('popstate', onPop)
    return () => {
      document.removeEventListener('click', onClick)
      window.removeEventListener('popstate', onPop)
    }
  }, [])

  // The page scrolls inside the shell, so the outline has to be told which box
  // moves: left to find one itself it tracks the window, which never scrolls
  // here, and every heading reads as "above the line".
  const column = useRef<HTMLDivElement>(null)

  const items = useMemo(
    () => toc.map(({ id, text, level }) => ({ id, label: text, level })),
    [toc],
  )

  // The reading order itself, which is the same list wherever it is put: the
  // column beside a book page, or the drawer a demo page opens over itself.
  //
  // **Closed by default, and the section you are reading is open.** Twenty-three
  // names in six groups is a list you scroll rather than scan, and most of it is
  // somewhere you are not.
  //
  // `multiple`, so opening one section does not shut the one you were comparing it
  // against — the groups are a reading order rather than a set of alternatives, and
  // "how it works" beside "the model" is a reasonable thing to want.
  //
  // Controlled rather than `defaultValue`, because the shell does not remount
  // between pages: a reader who arrives somewhere by the pager, by search or by a
  // link in the prose would otherwise find the nav still opened at wherever they
  // started. Arriving *adds* that group rather than replacing what is open, so the
  // navigation never closes a section the reader opened on purpose.
  const readingOrder = (
    <CollapsibleGroup
      type="multiple"
      value={openGroups}
      onChange={(next) => setOpenGroups(Array.isArray(next) ? next : [next])}
    >
      <VStack gap={1} width="100%" className="reading-order">
        {GROUPS.map((group) => (
          <Collapsible
            key={group.label}
            value={group.label}
            trigger={
              <Text type="label" weight="semibold" color="secondary">
                {group.label}
              </Text>
            }
          >
            <VStack gap={0} width="100%">
              {group.pages.map((page) => (
                <SideNavItem
                  key={page.slug}
                  label={page.title}
                  href={route(page.slug)}
                  isSelected={page.slug === slug}
                />
              ))}
            </VStack>
          </Collapsible>
        ))}
      </VStack>
    </CollapsibleGroup>
  )

  return (
    <ScrollRootProvider value={column}>
      {/* `fill` for both shapes: the shell owns the viewport and the content
          column scrolls inside it, which is what keeps the reading order and
          the outline in place while a page moves under them. */}
      <AppShell
        height="fill"
        contentPadding={0}
        variant="section"
        topNav={
          <TopNav
            label="Site"
            // The burger rides in the heading slot rather than `startContent`,
            // which sounds like the wrong one until you look at the bar: TopNav
            // renders the heading first and `startContent` after it, because that
            // slot is for the nav items a brand is followed by. A burger belongs
            // before the name, where the column it opens used to start — so it
            // goes in the heading, and the two travel together.
            //
            // On the pages that dropped that column, and only those: a book page
            // has the real thing a few pixels below, and two ways into one list
            // is one too many.
            heading={
              <HStack gap={2} align="center">
                {fills ? (
                  <IconButton
                    variant="ghost"
                    size="sm"
                    icon={<Icon icon="menu" color="inherit" />}
                    label="Open the reading order"
                    onClick={() => setNavOpen(true)}
                    aria-expanded={navOpen}
                    data-testid="nav"
                  />
                ) : null}
                <TopNavHeading
                  heading="Fjord DB"
                  subheading={phone ? undefined : 'An embedded, immutable fact database'}
                  headingHref={route('index')}
                />
              </HStack>
            }
            endContent={
              <HStack gap={2} align="center">
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => setSearching(true)}
                  label="Search"
                  endContent={phone ? undefined : <Kbd keys="/" />}
                  data-testid="search"
                />
                <IconButton
                  variant="ghost"
                  size="sm"
                  icon={<ContrastIcon width={18} height={18} />}
                  label="Toggle colour scheme"
                  onClick={onToggleMode}
                  data-testid="mode"
                />
              </HStack>
            }
          />
        }
        // Collapsible, not resizable: the reading order is a fixed list of
        // twenty-three names, so the only width worth choosing is none at all.
        //
        // A full-screen demo has no such column, at any width. The workbench and
        // the code browser are applications that own the viewport, and a list of
        // links to somewhere else takes width from the one thing such a page
        // exists to show — on a wide screen most of all, where there is room for
        // the nav and so nothing to make the choice for us. `undefined` is the
        // shape that means "no nav": a `SideNav` rendering nothing still reads to
        // AppShell as one that exists.
        sideNav={fills ? undefined : <SideNav collapsible>{readingOrder}</SideNav>}
        // Where the reading order goes instead — the same names, over the page
        // rather than beside it, on a burger in the bar.
        //
        // This is the drawer as a *node*, which is the only shape of AppShell's
        // three that answers at every width. The config object's drawer is gated
        // on `isBelowBreakpoint`, and the widest breakpoint it takes is `lg`
        // (1024) — `none` reads as `max-width: 0px`, meaning never rather than
        // always. A node passed here is rendered unconditionally, and in exchange
        // AppShell stops managing the state, so the open flag and the button that
        // sets it are both ours.
        mobileNav={
          fills ? (
            <MobileNav
              isOpen={navOpen}
              onOpenChange={setNavOpen}
              header="Fjord DB"
              label="The reading order"
            >
              {readingOrder}
            </MobileNav>
          ) : undefined
        }
      >
        {fills ? (
          children
        ) : (
          /* **The outline scrolls with the page, and sticks.**

              It was a `LayoutPanel` in the `end` slot, which is a *pane*: its own
              region, its own scroll, and therefore the content region's scrollbar
              drawn down the middle of the page — between the prose and the list of
              its own headings, which is the one place a scrollbar cannot mean
              anything. A pane is the right shape for an inspector, whose content
              is unrelated to the content beside it and as long. This is a dozen
              links about the very page it sits next to.

              So it moves inside the one scroll region and holds its place with
              `position: sticky`. `scrollContainerRef` is unchanged and still
              right: the scroll root is still this `LayoutContent`, and the outline
              is now a child of it rather than its neighbour. */
          <Panes
            content={
              <LayoutContent padding={0} ref={column}>
                <Center axis="horizontal">
                  <HStack gap={0} align="start" width="100%" className="page-frame">
                    {children}
                    {items.length > 1 && roomForTheOutline && (
                      <VStack className="page-outline" paddingBlock={8} padding={4}>
                        {/* The bar overlays the top of the scroll root, so the
                            outline has to land headings below it rather than
                            under it. */}
                        <Outline
                          items={items}
                          density="compact"
                          offset={24}
                          scrollContainerRef={column}
                          label="On this page"
                        />
                      </VStack>
                    )}
                  </HStack>
                </Center>
              </LayoutContent>
            }
          />
        )}
      </AppShell>

      <Search isOpen={searching} onOpenChange={setSearching} />
    </ScrollRootProvider>
  )
}
