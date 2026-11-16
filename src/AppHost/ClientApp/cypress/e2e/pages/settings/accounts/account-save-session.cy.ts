import { route, headers } from '@fixtures';
import { PlexAccountPaths, PlexLibraryPaths, PlexServerPaths } from '@api-urls';
import { generateResultDTO } from '@mock';
import type { PlexAccountDTO } from '@dto';

describe('Plex account save session ownership', () => {
	let accounts: PlexAccountDTO[];
	const pendingSaves = new Map<number, () => void>();

	beforeEach(() => {
		cy.basePageSetup({
			seed: 109,
			isLoggedIn: true,
			plexAccountCount: 2,
			plexServerCount: 1,
			override: {
				plexAccounts: (items) => items.map((account) => ({
					...account,
					displayName: `Original account ${account.id}`,
					username: `account-user-${account.id}`,
					password: 'OriginalPassword123!',
					hasPassword: true,
					customAuthenticationToken: '',
					isEnabled: true,
					isValidated: true,
					is2Fa: false,
				})),
			},
		}).then(({ plexAccounts }) => {
			accounts = plexAccounts.map((account) => ({ ...account }));
			cy.intercept('GET', PlexAccountPaths.getAllPlexAccountsEndpoint(), (request) => {
				request.reply({ statusCode: 200, body: generateResultDTO(accounts), ...headers });
			});
			cy.intercept('PUT', PlexAccountPaths.updatePlexAccountByIdEndpoint(), (request) => {
				const current = accounts.find((account) => account.id === request.body.id);
				if (!current) throw new Error('Expected an update for an account in the fixture');
				const { promise, resolve } = Promise.withResolvers<undefined>();
				pendingSaves.set(current.id, () => {
					const updated: PlexAccountDTO = { ...current, ...request.body };
					accounts = accounts.map((account) => account.id === updated.id ? updated : account);
					pendingSaves.delete(current.id);
					request.reply({ statusCode: 200, body: generateResultDTO(updated), ...headers });
					resolve(undefined);
				});
				return promise;
			}).as('updateAccount');
			cy.visit(route('/settings/accounts'));
		});
	});

	afterEach(() => {
		pendingSaves.forEach((release) => release());
		pendingSaves.clear();
	});

	it('Should finish an old save without closing or unlocking the newer pending account dialog', () => {
		cy.getPageData().then(({ plexAccounts, plexServers, plexLibraries }) => {
			const [first, second] = plexAccounts;
			if (!first || !second) throw new Error('Expected two accounts in the fixture');
			cy.get('@updateAccount.all').should('have.length', 0);

			cy.getCy(`account-card-id-${first.id}`).should('be.visible').click();
			cy.getCy('account-form-display-name-input').clear().type('First saved account');
			cy.getCy('account-dialog-update-button').should('not.be.disabled').click();
			cy.wrap(null).should(() => expect(pendingSaves.has(first.id)).to.equal(true));
			cy.getCy('account-dialog-update-button').find('.q-spinner').should('be.visible');
			cy.getCy('account-dialog-form').within(() => cy.getCy('dialog-close-button').click());
			cy.getCy('account-dialog-form').should('not.exist');

			cy.getCy(`account-card-id-${second.id}`).should('be.visible').click();
			cy.getCy('account-form-display-name-input').clear().type('Second saved account');
			cy.getCy('account-dialog-update-button').should('not.be.disabled').click();
			cy.wrap(null).should(() => expect(pendingSaves.has(second.id)).to.equal(true));
			cy.getCy('account-dialog-update-button').find('.q-spinner').should('be.visible');

			// Observe every refresh that must complete before the old save emits to the dialog.
			cy.intercept('GET', PlexAccountPaths.getAllPlexAccountsEndpoint(), (request) => {
				request.reply({ statusCode: 200, body: generateResultDTO(accounts), ...headers });
			}).as('refreshAccounts');
			cy.intercept('GET', PlexServerPaths.getAllPlexServersEndpoint(), {
				statusCode: 200, body: generateResultDTO(plexServers), ...headers,
			}).as('refreshServers');
			cy.intercept('GET', PlexLibraryPaths.getAllPlexLibrariesEndpoint(), {
				statusCode: 200, body: generateResultDTO(plexLibraries), ...headers,
			}).as('refreshLibraries');
			cy.then(() => pendingSaves.get(first.id)!());
			cy.wait('@updateAccount').then(({ request, response }) => {
				expect(request.body).to.include({ id: first.id, displayName: 'First saved account' });
				expect(response?.statusCode).to.equal(200);
			});
			cy.wait(['@refreshAccounts', '@refreshServers', '@refreshLibraries']);
			cy.getCy('account-dialog-form').should('be.visible');
			cy.getCy('account-form-display-name-input').should('have.value', 'Second saved account');
			cy.getCy('account-form-username-input').should('have.value', second.username);
			cy.getCy('account-dialog-update-button').find('.q-spinner').should('be.visible');

			cy.then(() => pendingSaves.get(second.id)!());
			cy.wait('@updateAccount').then(({ request, response }) => {
				expect(request.body).to.include({ id: second.id, displayName: 'Second saved account' });
				expect(response?.statusCode).to.equal(200);
			});
			cy.wait(['@refreshAccounts', '@refreshServers', '@refreshLibraries']);
			cy.getCy('account-dialog-form').should('not.exist');
			cy.getCy(`account-card-id-${first.id}`).should('contain.text', 'First saved account');
			cy.getCy(`account-card-id-${second.id}`).should('contain.text', 'Second saved account');
			cy.get('@updateAccount.all').should('have.length', 2);
			cy.reload();
			cy.getPageData();
			cy.getCy(`account-card-id-${first.id}`).should('contain.text', 'First saved account');
			cy.getCy(`account-card-id-${second.id}`).should('contain.text', 'Second saved account');
		});
	});
});
