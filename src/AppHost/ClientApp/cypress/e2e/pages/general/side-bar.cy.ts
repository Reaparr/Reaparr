import { DialogType } from '@enums';
import { PlexServerPaths } from '@api-urls';
import { generateRefreshPlexAccountAccessRapportDTO, generateResultDTO } from '@mock';

describe('Side bar', () => {
	it('Should display no library warning and allow the user to refresh the Plex account access when a server has no libraries', () => {
		cy.basePageSetup({
			plexAccountCount: 1,
			plexServerCount: 1,
			plexMovieLibraryCount: 0,
			plexTvShowLibraryCount: 0,
		});
		cy.visitEmptyPage();

		cy.getPageData().then(({ plexAccounts, plexServers, plexLibraries }) => {
			const data = generateRefreshPlexAccountAccessRapportDTO({
				plexAccounts, plexLibraries, plexServers,
			});

			for (const server of plexServers) {
				cy.intercept('POST', PlexServerPaths.refreshPlexServerAccountsAccessEndpoint(server.id), {
					statusCode: 200,
					body: generateResultDTO(data), delay: 200,
				});

				cy.getCy(`server-drawer-item-${server.id}`).click();
				cy.getCy(`server-drawer-item-${server.id}-no-libraries`).click();

				cy.getCy(DialogType.RefreshAccountAccessDialog).should('be.visible');
				cy.getCy('refresh-account-access-dialog-hide-btn').click();
				cy.getCy(`server-drawer-item-${server.id}-refresh-loading`).should('not.exist');
			}
		});
	});

	it('Should strike through a server when all of its libraries are inaccessible', () => {
		cy.basePageSetup({
			plexAccountCount: 1,
			plexServerCount: 1,
			plexMovieLibraryCount: 1,
			plexTvShowLibraryCount: 0,
			override: {
				plexAccounts: (accounts) => accounts.map((account) => ({ ...account, plexServerAccess: [], plexLibraryAccess: [] })),
			},
		});
		cy.visitEmptyPage();

		cy.getPageData().then(({ plexServers }) => {
			const server = plexServers[0]!;

			cy.getCy(`server-drawer-item-${server.id}`)
				.find('.server-name-text')
				.should('have.class', 'inaccessible-item-text')
				.and('have.css', 'text-decoration-line', 'line-through');
		});
	});
});
