import { PlexServerPaths } from '@api-urls';
import { headers } from '@fixtures';
import { generateFailedResultDTO, generateResultDTO } from '@mock';

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

		for (let i = 1; i <= 6; i++) {
			cy.getCy('server-dialog-tab-' + i).click();
			cy.getCy('server-dialog-tab-content-' + i)
				.should('exist')
				.and('be.visible');
		}

		cy.getCy('server-stats-export-image').should('be.visible');
		cy.getCy('server-stats-summary-tab').then(($tab) => {
			const top = $tab[0]!.getBoundingClientRect().top;
			cy.getCy('server-stats-content').scrollTo('bottom');
			cy.getCy('server-stats-summary-tab').should(($scrolledTab) => {
				expect($scrolledTab[0]!.getBoundingClientRect().top, 'stats tabs stay fixed').to.equal(top);
			});
		});
		cy.getCy('server-dialog-tab-6').should('be.visible');
		cy.getCy('server-stats-export-image').should('be.visible');
		cy.getCy('server-dialog-tab-1').click();
		cy.getCy('server-stats-export-image').should('not.exist');
		cy.getCy('server-dialog-tab-6').click();
		cy.getCy('server-stats-export-image').should('be.visible');

		cy.getCy('server-dialog-close-btn').click();
		cy.getCy('server-dialog-cy').should('not.exist');

		cy.getCy('server-dialog-2').filter(':visible').first().click();
		cy.getCy('server-dialog-tab-6').click();
		cy.getCy('server-stats-export-image').should('be.visible');
		cy.getCy('server-dialog-close-btn').click();
		cy.getCy('server-dialog-cy').should('not.exist');
	});

	it('Should keep server settings and confirmation open after disabling fails and recover on retry', () => {
		// Arrange: keep subsequent reads consistent with the successful retry.
		cy.getPageData().then((data) => {
			let servers = data.plexServers;
			const target = servers.find((server) => server.isEnabled);
			expect(target, 'enabled fixture server').not.to.equal(undefined);
			const serverId = target!.id;
			let attempts = 0;
			cy.intercept('GET', PlexServerPaths.getAllPlexServersEndpoint(), (request) => {
				request.reply({ statusCode: 200, body: generateResultDTO(servers), ...headers });
			});
			cy.intercept('PUT', PlexServerPaths.setServerEnabledEndpoint(serverId), (request) => {
				expect(request.body).to.deep.equal({ isEnabled: false });
				attempts++;
				if (attempts === 1) {
					request.reply({
						statusCode: 500,
						body: generateFailedResultDTO({
							statusCode: 500,
							errors: [{ message: 'Disabling failed', metadata: {}, reasons: [] }],
						}),
						...headers,
					});
					return;
				}
				servers = servers.map((server) => server.id === serverId ? { ...server, isEnabled: false } : server);
				request.reply({
					statusCode: 200, body: generateResultDTO(servers.find((server) => server.id === serverId)!), ...headers,
				});
			}).as('disableServer');

			cy.getCy(`server-drawer-item-${serverId}`).filter(':visible').first()
				.closest('.q-expansion-item').find('[data-cy^="server-dialog-"]').click();
			cy.getCy('server-dialog-tab-3').click();
			cy.getCy('server-enabled-toggle').should('have.attr', 'aria-checked', 'true').click();

			// Act
			cy.getCy('confirmation-dialog-confirmation-button').click();
			cy.wait('@disableServer').its('response.statusCode').should('equal', 500);
			cy.getCy('alert-dialog').should('be.visible').and('contain.text', 'Disabling failed');
			cy.getCy('close-alert-dialog').click();

			// Assert: failure leaves both dialogs available and retry enabled.
			cy.getCy('confirmation-dialog').should('be.visible');
			// Settings stay mounted behind the foreground confirmation modal.
			cy.getCy('server-dialog-cy').should('exist');
			cy.getCy('server-enabled-toggle').should('have.attr', 'aria-checked', 'true');
			cy.getCy('confirmation-dialog-confirmation-button').should('not.be.disabled').click();
			cy.wait('@disableServer').its('response.statusCode').should('equal', 200);
			cy.getCy('confirmation-dialog').should('not.exist');
			cy.getCy('server-dialog-cy').should('not.exist');
			cy.reload();
			cy.getCy('page-load-completed', { timeout: 20000 }).should('be.visible');
			cy.getCy(`server-drawer-item-${serverId}`).should('not.exist');
		});
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
			cy.get('.server-dialog-tabs [role="tab"]').should(($tabs) => {
				const first = $tabs[0]!.getBoundingClientRect();
				const second = $tabs[1]!.getBoundingClientRect();
				if (viewport.width <= 599) {
					expect(second.top, 'mobile tabs share a row').to.be.closeTo(first.top, 1);
					expect(second.left, 'mobile tabs run horizontally').to.be.gte(first.right - 1);
				} else {
					expect(second.left, 'desktop tabs share a column').to.be.closeTo(first.left, 1);
					expect(second.top, 'desktop tabs run vertically').to.be.gte(first.bottom - 1);
				}
			});
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

			for (let i = 1; i <= 6; i++) {
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
