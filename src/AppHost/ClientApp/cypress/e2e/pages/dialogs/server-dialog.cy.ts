describe('Reaparr Server Dialog', () => {
	beforeEach(() => {
		cy.basePageSetup({
			plexAccountCount: 1,
			plexServerCount: 5,
			plexMovieLibraryCount: 5,
		});

		cy.visitEmptyPage();
		cy.getCy('page-load-completed', { timeout: 20000 }).should('be.visible');
	});

	it('Should navigate the server dialog tabs when the navigation tabs are used and then close again', () => {
		cy.getCy('server-dialog-2').filter(':visible').first().click();

		for (let i = 1; i <= 5; i++) {
			cy.getCy('server-dialog-tab-' + i).click();
			cy.getCy('server-dialog-tab-content-' + i)
				.should('exist')
				.and('be.visible');
		}

		cy.getCy('server-dialog-close-btn').click();
		cy.getCy('server-dialog-cy').should('not.exist');
	});

	const responsiveViewports = [
		{ width: 320, height: 568 },
		{ width: 568, height: 320 },
		{ width: 768, height: 1024 },
		{ width: 1280, height: 800 },
		{ width: 1920, height: 1080 },
	] as const;

	for (const viewport of responsiveViewports) {
		it(`keeps every server settings tab usable at ${viewport.width}x${viewport.height}`, () => {
			cy.viewport(viewport.width, viewport.height);
			if (viewport.width <= 1023) {
				cy.getCy('navigation-drawer-toggle')
					.filter(':visible')
					.first()
					.should('have.attr', 'aria-expanded', 'false');
				cy.getCy('navigation-drawer-toggle').filter(':visible').first().click();
				cy.getCy('navigation-drawer-toggle')
					.filter(':visible')
					.first()
					.should('have.attr', 'aria-expanded', 'true');
				cy.get('.navigation-drawer').should('be.visible');
			}
			cy.getCy('server-dialog-2').filter(':visible').first().scrollIntoView().click();
			cy.getCy('server-dialog-cy').should('be.visible').then(($dialog) => {
				const dialogRect = $dialog[0]!.getBoundingClientRect();
				expect(dialogRect.left, 'dialog starts inside viewport').to.be.gte(0);
				expect(dialogRect.right, 'dialog ends inside viewport').to.be.lte(viewport.width);
				cy.get('.server-dialog-tabs').then(($tabs) => {
					const tabsRect = $tabs[0]!.getBoundingClientRect();
					expect(tabsRect.left, 'tab bar starts inside dialog').to.be.gte(dialogRect.left);
					expect(tabsRect.right, 'tab bar ends inside dialog').to.be.lte(dialogRect.right);
					if (viewport.width <= 599)
						expect(tabsRect.height, 'mobile tab bar height').to.be.lte(48);
				});
			});

			for (let i = 1; i <= 5; i++) {
				cy.getCy('server-dialog-tab-' + i)
					.should('have.attr', 'aria-label');
				cy.getCy('server-dialog-tab-' + i).click();
				cy.getCy('server-dialog-tab-' + i)
					.should('have.attr', 'aria-selected', 'true');
				cy.getCy('server-dialog-tab-content-' + i).should('exist');
				cy.get('.tab-content').then(($content) => {
					const content = $content[0]!;
					const contentRect = content.getBoundingClientRect();
					expect(contentRect.height, `tab ${i} content height`).to.be.greaterThan(0);
					expect(contentRect.left, `tab ${i} content left`).to.be.gte(0);
					expect(contentRect.right, `tab ${i} content right`).to.be.lte(viewport.width);
					expect(content.scrollWidth, `tab ${i} horizontal overflow`).to.be.lte(content.clientWidth + 1);
				});
			}

			if (viewport.width <= 599) {
				cy.getCy('server-dialog-tab-2').click();
				cy.get('.server-connection-row__radio').first().should(($radio) => {
					const rect = $radio[0]!.getBoundingClientRect();
					expect(rect.width, 'preferred connection touch width').to.be.gte(44);
					expect(rect.height, 'preferred connection touch height').to.be.gte(44);
				});
			}

			if (viewport.width <= 1023) {
				cy.getCy('server-dialog-tab-5').click();
				cy.get('.server-command-button').first().scrollIntoView();
				cy.get('.server-command-button:visible').each(($button) => {
					expect($button[0]!.getBoundingClientRect().height, 'command touch height').to.be.gte(43.9);
				});
			}

			cy.getCy('server-dialog-close-btn').should('be.visible').click();
			cy.getCy('server-dialog-cy').should('not.exist');
		});
	}
});
