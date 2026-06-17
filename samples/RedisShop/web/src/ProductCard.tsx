import type { Product } from './types.ts';

export function ProductCard({ product }: { product: Product }) {
  return (
    <article className="card">
      <div className="card-thumb" aria-hidden>
        {product.name.charAt(0)}
      </div>
      <div className="card-body">
        <span className="card-category">{product.category}</span>
        <h3 className="card-name">{product.name}</h3>
        <p className="card-desc">{product.description}</p>
        <div className="card-foot">
          <span className="card-price">${product.price.toFixed(2)}</span>
          <button className="card-buy" disabled={!product.inStock}>
            {product.inStock ? 'Add to cart' : 'Out of stock'}
          </button>
        </div>
      </div>
    </article>
  );
}
