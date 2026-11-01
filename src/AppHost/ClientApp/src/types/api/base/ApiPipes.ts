import Log from 'consola';
import { Observable, catchError, of } from 'rxjs';
import { map, take } from 'rxjs/operators';
import type { AxiosRequestConfig, AxiosResponse } from 'axios';
import Axios from 'axios';
import type { BaseResultDTO, ErrorDTO } from '@dto';
import type { ResultDTO } from '@interfaces';

const activeApiRequests = new Set<AbortController>();
let apiRequestSuspensions = 0;

export function suspendApiRequests(): () => void {
	apiRequestSuspensions += 1;
	for (const controller of activeApiRequests) controller.abort();
	activeApiRequests.clear();
	let resumed = false;

	return () => {
		if (resumed) return;
		resumed = true;
		apiRequestSuspensions -= 1;
	};
}

export function axiosObservable<T>(config: AxiosRequestConfig): Observable<AxiosResponse<T>> {
	return new Observable<AxiosResponse<T>>((subscriber) => {
		if (apiRequestSuspensions > 0) {
			subscriber.complete();
			return;
		}

		const controller = new AbortController();
		activeApiRequests.add(controller);

		Axios.request<T>({
			...config,
			signal: controller.signal,
		})
			.then((res) => {
				if (!controller.signal.aborted) subscriber.next(res);
				subscriber.complete();
			})
			.catch((err) => {
				if (controller.signal.aborted || err?.code === 'ERR_CANCELED') {
					subscriber.complete();
				} else {
					subscriber.error(err);
				}
			});

		return () => {
			activeApiRequests.delete(controller);
			controller.abort();
		};
	});
}

export function apiCheckPipe<T extends object = BaseResultDTO>(
	source$: Observable<AxiosResponse<T>>,
): Observable<T extends BaseResultDTO ? BaseResultDTO : ResultDTO<T>> {
	return source$.pipe(
		map((res) => {
			if (res.status === 204 || res.data == null) {
				return {
					isSuccess: true,
					statusCode: res.status,
					errors: [],
					successes: [],
				} as unknown as T extends BaseResultDTO ? BaseResultDTO : ResultDTO<T>;
			}

			// Handle generic ResultDTO<T> case
			if (Object.hasOwn(res.data, 'value')) {
				return toResultDTO<T>(res) as unknown as T extends BaseResultDTO ? BaseResultDTO : ResultDTO<T>;
			}

			// Handle BaseResultDTO case
			return res.data as unknown as T extends BaseResultDTO ? BaseResultDTO : ResultDTO<T>;
		}),
		catchError((error) => {
			Log.error('Error in API call', error);

			// Convert error response to ResultDTO
			return of(toResultDTO<never>(error.response)) as Observable<
				T extends BaseResultDTO ? BaseResultDTO : ResultDTO<T>
			>;
		}),
		// Ensure we complete any API calls after the response has been received
		take(1),
	);
}

// Convert AxiosResponse to ResultDTO<T>
function toResultDTO<T = void>(res?: AxiosResponse): ResultDTO<T> {
	if (!res) {
		const error: ErrorDTO = {
			message: 'Internal Server Error',
			reasons: [],
			metadata: {},
		};
		return {
			isSuccess: false,
			errors: [error],
			successes: [],
			statusCode: 999,
		};
	}

	const result = res.data as ResultDTO<T>;
	return {
		isSuccess: result.isSuccess,
		errors: result.errors,
		successes: result.successes,
		value: result.value,
		statusCode: res.status,
	};
}
