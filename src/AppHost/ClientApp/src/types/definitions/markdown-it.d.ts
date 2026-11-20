declare module 'markdown-it' {
	export type Options = Record<string, unknown>;

	export interface Token {
		attrs: [string, string][] | null;
		attrIndex(name: string): number;
		attrPush(attribute: [string, string]): void;
	}

	export interface Renderer {
		rules: {
			link_open?: RendererRule;
			[name: string]: RendererRule | undefined;
		};
		renderToken(tokens: Token[], index: number, options: Options): string;
	}

	export type RendererRule = (
		tokens: Token[],
		index: number,
		options: Options,
		env: unknown,
		renderer: Renderer,
	) => string;

	export interface MarkdownItInstance {
		renderer: Renderer;
	}

	export type PluginSimple = (markdownIt: MarkdownItInstance) => void;

	class MarkdownIt implements MarkdownItInstance {
		constructor(options?: Options);
		renderer: Renderer;
	}

	export default MarkdownIt;
}
