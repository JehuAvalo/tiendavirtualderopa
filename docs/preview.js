const slides = [
  { theme: 'rose', main: ['assets/dress-2-only.jpg', 'Vestido floral para niña'], left: ['assets/item-04.jpg', 'Polo rosa con flor bordada'], right: ['assets/item-20.jpg', 'Falda de denim con botones'], eyebrow: 'LA COLECCIÓN DE BABY GIRLIE', title: 'Ropa para<br><em>cada día.</em>', description: 'Vestidos, conjuntos y favoritos para combinar a su manera.' },
  { theme: 'cream', main: ['assets/dress-1-only.jpg', 'Vestido rosa de tul con lazo'], left: ['assets/dress-2-only.jpg', 'Vestido floral para niña'], right: ['assets/dress-3-only.jpg', 'Vestido bordado marfil'], eyebrow: 'PARA SUS DÍAS ESPECIALES', title: 'Un vestido<br><em>para recordar.</em>', description: 'Modelos para celebrar esos momentos que se guardan siempre.' },
  { theme: 'sage', main: ['assets/item-24.jpg', 'Conjunto verde de blusa y short'], left: ['assets/item-25.jpg', 'Conjunto amarillo de cuadros'], right: ['assets/item-23.jpg', 'Conjunto deportivo rosa'], eyebrow: 'COMODIDAD QUE ACOMPAÑA', title: 'Lista para<br><em>salir a jugar.</em>', description: 'Conjuntos cómodos, alegres y fáciles de llevar.' }
];
let slideIndex = 0;
let products = [];
let selectedCategory = 'Todos';
const $ = (selector) => document.querySelector(selector);

function showSlide(index) {
  slideIndex = (index + slides.length) % slides.length;
  const slide = slides[slideIndex];
  $('.banner-visual').dataset.theme = slide.theme;
  $('#banner-eyebrow').textContent = slide.eyebrow;
  $('#banner-title').innerHTML = slide.title;
  $('#banner-description').textContent = slide.description;
  for (const [id, item] of [['#hero-image', slide.main], ['#hero-left', slide.left], ['#hero-right', slide.right]]) {
    const image = $(id);
    image.src = item[0];
    image.alt = item[1];
  }
  document.querySelectorAll('#banner-dots button').forEach((dot, i) => {
    dot.classList.toggle('active', i === slideIndex);
    dot.setAttribute('aria-current', String(i === slideIndex));
  });
}

function renderCatalog() {
  const search = $('#catalog-search').value.trim().toLocaleLowerCase('es');
  const size = $('#catalog-size').value;
  let visible = products.filter((item) =>
    (selectedCategory === 'Todos' || item.category === selectedCategory) &&
    (!size || item.sizes.includes(size)) &&
    (!search || `${item.name} ${item.category} ${item.detail}`.toLocaleLowerCase('es').includes(search))
  );
  if ($('#catalog-sort').value === 'name') visible = [...visible].sort((a, b) => a.name.localeCompare(b.name, 'es'));
  $('#result-count').textContent = `${visible.length} ${visible.length === 1 ? 'prenda' : 'prendas'}`;
  const grid = $('#products');
  grid.replaceChildren();
  if (!visible.length) {
    const empty = document.createElement('p');
    empty.className = 'catalog-empty';
    empty.textContent = 'No encontramos prendas con esos filtros.';
    grid.append(empty);
    return;
  }
  for (const item of visible) {
    const card = document.createElement('article');
    card.className = 'product';
    const imageButton = document.createElement('button');
    imageButton.className = 'product-image';
    imageButton.type = 'button';
    imageButton.setAttribute('aria-label', `Ver ${item.name}`);
    const image = document.createElement('img');
    image.src = item.image;
    image.alt = item.name;
    image.loading = 'lazy';
    imageButton.append(image);
    imageButton.addEventListener('click', () => openDetail(item));
    const info = document.createElement('div');
    info.className = 'product-info';
    const name = document.createElement('h3');
    name.textContent = item.name;
    const category = document.createElement('p');
    category.textContent = item.category;
    const detailButton = document.createElement('button');
    detailButton.className = 'view';
    detailButton.type = 'button';
    detailButton.textContent = 'Ver prenda';
    detailButton.addEventListener('click', () => openDetail(item));
    info.append(name, category, detailButton);
    card.append(imageButton, info);
    grid.append(card);
  }
}

function openDetail(item) {
  const content = $('#detail-content');
  content.replaceChildren();
  const image = document.createElement('img');
  image.src = item.image;
  image.alt = item.name;
  const copy = document.createElement('div');
  const category = document.createElement('p');
  category.className = 'preview-detail-category';
  category.textContent = item.category;
  const title = document.createElement('h2');
  title.id = 'detail-title';
  title.textContent = item.name;
  const description = document.createElement('p');
  description.textContent = item.detail;
  const sizes = document.createElement('p');
  sizes.className = 'preview-detail-sizes';
  sizes.textContent = `Tallas de muestra: ${item.sizes.join(' · ')}`;
  const note = document.createElement('p');
  note.className = 'preview-detail-note';
  note.textContent = 'Imagen y modelo de demostración.';
  copy.append(category, title, description, sizes, note);
  content.append(image, copy);
  $('#detail').showModal();
}

async function init() {
  $('#year').textContent = new Date().getFullYear();
  $('#banner-dots').replaceChildren(...slides.map((_, index) => {
    const dot = document.createElement('button');
    dot.type = 'button';
    dot.setAttribute('aria-label', `Mostrar imagen ${index + 1}`);
    dot.addEventListener('click', () => showSlide(index));
    return dot;
  }));
  $('#banner-prev').addEventListener('click', () => showSlide(slideIndex - 1));
  $('#banner-next').addEventListener('click', () => showSlide(slideIndex + 1));
  showSlide(0);
  document.querySelectorAll('[data-filter]').forEach((button) => button.addEventListener('click', () => {
    selectedCategory = button.dataset.filter;
    document.querySelectorAll('[data-filter]').forEach((other) => other.classList.toggle('active', other === button));
    renderCatalog();
  }));
  for (const selector of ['#catalog-search', '#catalog-size', '#catalog-sort']) $(selector).addEventListener('input', renderCatalog);
  $('#clear-filters').addEventListener('click', () => {
    selectedCategory = 'Todos';
    $('#catalog-search').value = '';
    $('#catalog-size').value = '';
    $('#catalog-sort').value = 'default';
    document.querySelectorAll('[data-filter]').forEach((button) => button.classList.toggle('active', button.dataset.filter === 'Todos'));
    renderCatalog();
  });
  $('#detail-close').addEventListener('click', () => $('#detail').close());
  $('#detail').addEventListener('click', (event) => { if (event.target === $('#detail')) $('#detail').close(); });
  try {
    const response = await fetch('catalog.json');
    if (!response.ok) throw new Error('Catálogo no disponible');
    products = await response.json();
    renderCatalog();
  } catch {
    $('#products').textContent = 'No se pudo cargar la colección. Inténtalo de nuevo más tarde.';
  }
}
init();
