import type { ResultDTO } from '@interfaces';

export interface IApiError {
	method: string;
	url: string;
	statusCode: number;
	code?: string;
	message: string;
}

export interface IAlert {
	id: number;
	title: string;
	text: string;
	result?: ResultDTO;
	apiErrors?: IApiError[];
	hasOmittedApiErrors?: boolean;
}
