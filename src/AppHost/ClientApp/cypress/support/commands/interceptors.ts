import { PlexAccountPaths } from '@api-urls';
import { generatePlexAccount, generateResultDTO } from '@mock';
import type { PlexAccountDTO, ValidatePlexCredentialsDTO, ValidatePlexTokenEndpointResponse } from '@dto';

/* eslint-disable @typescript-eslint/no-namespace */
declare global {
	namespace Cypress {
		// 🤔 unsure why this Subject is unused, nor what to do with it...
		// eslint-disable-next-line @typescript-eslint/no-unused-vars
		interface Chainable<Subject> {

			validatePlexCredentialsEndpoint(config: {
				isUnAuthorized?: boolean; partialData?: Partial<PlexAccountDTO>;
			}): Chainable;

			validatePlexTokenEndpoint(config?: {
				isUnAuthorized?: boolean; partialData?: Partial<PlexAccountDTO>;
			}): Chainable;
		}
	}
}

Cypress.Commands.add('validatePlexCredentialsEndpoint', ({ isUnAuthorized = false, partialData = {} }: {
	isUnAuthorized?: boolean; partialData?: Partial<ValidatePlexCredentialsDTO>;
}) => {
	const plexAccount: PlexAccountDTO = generatePlexAccount({ id: 99 });

	return cy.intercept('POST', PlexAccountPaths.validatePlexCredentialsEndpoint(), {
		statusCode: 200, body: generateResultDTO<ValidatePlexCredentialsDTO>({
			isUnAuthorized: isUnAuthorized,
			authenticationToken: plexAccount.authenticationToken,
			clientId: plexAccount.clientId,
			email: plexAccount.email,
			is2Fa: plexAccount.is2Fa,
			isValidated: plexAccount.isValidated,
			password: plexAccount.password,
			plexId: plexAccount.plexId,
			title: plexAccount.title,
			username: plexAccount.username,
			uuid: plexAccount.uuid,
			validatedAt: plexAccount.validatedAt, ...partialData,
		}),
	});
});

Cypress.Commands.add('validatePlexTokenEndpoint', ({ isUnAuthorized = false, partialData = {} }: {
	isUnAuthorized?: boolean; partialData?: Partial<ValidatePlexTokenEndpointResponse>;
} = {}) => {
	const plexAccount: PlexAccountDTO = generatePlexAccount({ id: 101 });

	return cy.intercept('POST', PlexAccountPaths.validatePlexTokenEndpoint(), {
		statusCode: 200, body: generateResultDTO<ValidatePlexTokenEndpointResponse>({
			isUnAuthorized: isUnAuthorized,
			customAuthenticationToken: plexAccount.customAuthenticationToken,
			clientId: plexAccount.clientId,
			email: plexAccount.email,
			is2Fa: plexAccount.is2Fa,
			isValidated: plexAccount.isValidated,
			plexId: plexAccount.plexId,
			title: plexAccount.title,
			username: plexAccount.username,
			uuid: plexAccount.uuid,
			validatedAt: plexAccount.validatedAt,
			...partialData,
		}),
	});
});
