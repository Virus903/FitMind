document.querySelectorAll(".calculator-tabs button").forEach(btn=>{
 btn.addEventListener("click",()=>{
   document.querySelectorAll(".calculator-tabs button").forEach(x=>x.classList.remove("active"));
   document.querySelectorAll(".calculator-card").forEach(x=>x.classList.add("hidden"));
   btn.classList.add("active");document.getElementById("calc-"+btn.dataset.calc)?.classList.remove("hidden");
 });
});
function fitmindBmi(){const w=parseFloat(document.getElementById("bmiWeight").value),h=parseFloat(document.getElementById("bmiHeight").value)/100;document.getElementById("bmiResult").textContent=(w>0&&h>0)?`ИМТ: ${(w/(h*h)).toFixed(1)}`:"Заполни вес и рост.";}
function fitmindCalories(){const w=parseFloat(document.getElementById("calWeight").value),k=parseFloat(document.getElementById("calActivity").value);document.getElementById("calResult").textContent=(w>0&&k>0)?`Ориентир: ${Math.round(w*k)} ккал/день`:"Заполни поля.";}
function fitmindWater(){const w=parseFloat(document.getElementById("waterWeight").value);document.getElementById("waterResult").textContent=w>0?`Ориентир: ${Math.round(w*30)} мл/день`:"Укажи вес.";}


// FitMind landing: плавный скролл + активная кнопка секции.
(() => {
  const links = [...document.querySelectorAll('[data-landing-nav]')];
  const sections = [...document.querySelectorAll('[data-landing-section]')];
  if (!links.length || !sections.length) return;

  const setActive = (id) => {
    links.forEach(link => link.classList.toggle('active', link.dataset.landingNav === id));
  };

  links.forEach(link => {
    link.addEventListener('click', event => {
      const id = link.dataset.landingNav;
      const target = document.getElementById(id);
      if (!target) return;
      event.preventDefault();
      setActive(id);
      target.scrollIntoView({ behavior: 'smooth', block: 'start' });
      history.replaceState(null, '', `#${id}`);
    });
  });

  const observer = new IntersectionObserver(entries => {
    const visible = entries
      .filter(entry => entry.isIntersecting)
      .sort((a, b) => b.intersectionRatio - a.intersectionRatio)[0];
    if (visible) setActive(visible.target.id);
  }, { rootMargin: '-25% 0px -55% 0px', threshold: [0.05, 0.2, 0.45] });

  sections.forEach(section => observer.observe(section));

  if (location.hash) {
    const id = location.hash.slice(1);
    if (sections.some(x => x.id === id)) setActive(id);
  }
})();

document.addEventListener('DOMContentLoaded',()=>{
  document.querySelectorAll('select[data-current]').forEach(s=>{ if(s.dataset.current) s.value=s.dataset.current; });
});

// Доступное меню на узком экране и безопасная замена отсутствующих фотографий.
document.querySelector('.mobile-menu')?.addEventListener('click', function () {
 const opened = document.querySelector('.sidebar')?.classList.toggle('mobile-open');
 this.setAttribute('aria-expanded', String(!!opened));
});
document.addEventListener('keydown', e => { if(e.key==='Escape') {document.querySelector('.sidebar')?.classList.remove('mobile-open');document.querySelector('.mobile-menu')?.setAttribute('aria-expanded','false');} });
document.querySelectorAll('img').forEach(img => {
 img.loading='lazy';
 const fallback=()=>{if(!img.src.endsWith('/images/covers/unavailable.svg')) img.src='/images/covers/unavailable.svg';};
 img.addEventListener('error',fallback,{once:true});
 if(img.complete && img.naturalWidth===0)fallback();
});
// Только один источник пищевой ценности за раз.
['ProductId','RecipeId','UserFoodItemId'].forEach(id=>document.getElementById(id)?.addEventListener('change',e=>{
 if(e.target.value) ['ProductId','RecipeId','UserFoodItemId'].filter(x=>x!==id).forEach(x=>{const el=document.getElementById(x);if(el)el.value='';});
}));
// Единое подтверждение удаления без отправки формы до решения пользователя.
const confirmDialog=document.createElement('dialog');confirmDialog.className='fm-dialog';confirmDialog.innerHTML='<h2>Подтвердите действие</h2><p></p><div class="actions"><button type="button" class="ghost" data-cancel>Отмена</button><button type="button" class="danger" data-accept>Подтвердить</button></div>';document.body.append(confirmDialog);let pendingForm;
confirmDialog.querySelector('[data-cancel]').onclick=()=>{pendingForm=null;confirmDialog.close();};confirmDialog.querySelector('[data-accept]').onclick=()=>{const form=pendingForm;pendingForm=null;confirmDialog.close();if(form){form.dataset.confirmed='true';form.requestSubmit();}};
confirmDialog.addEventListener('cancel',()=>{pendingForm=null;});
document.querySelectorAll('form[data-confirm], .admin-main form:has(button.danger)').forEach(form=>form.addEventListener('submit',e=>{if(form.dataset.confirmed==='true')return;e.preventDefault();pendingForm=form;confirmDialog.querySelector('p').textContent=form.dataset.confirm||'Удалить запись? Действие нельзя отменить.';confirmDialog.showModal();}));
document.querySelectorAll('form[method="post"]').forEach(form=>form.addEventListener('submit',event=>{setTimeout(()=>{if(event.defaultPrevented)return;form.querySelectorAll('button:not([type="button"])').forEach(b=>{b.disabled=true;b.setAttribute('aria-busy','true');});},0);}));

document.querySelectorAll(".password-toggle").forEach(button => {
 button.addEventListener("click", () => {
  const field = button.closest(".auth-v04-input").querySelector("input");
  const show = field.type === "password";
  field.type = show ? "text" : "password";
  button.setAttribute("aria-label", show ? "Скрыть пароль" : "Показать пароль");
  button.setAttribute("aria-pressed", String(show));
 });
});

// Server-rendered errors should be discoverable without searching the form.
document.querySelector('.auth-v04-page .validation-summary-errors')?.focus();

document.querySelector(".mobile-close")?.addEventListener("click",()=>{document.querySelector(".sidebar")?.classList.remove("mobile-open");document.querySelector(".mobile-menu")?.setAttribute("aria-expanded","false");});
document.querySelector('.admin-menu-toggle')?.addEventListener('click',event=>{
 const button=event.currentTarget, navigation=document.getElementById('admin-navigation');
 const open=navigation?.classList.toggle('is-open')??false;button.setAttribute('aria-expanded',String(open));
});

// Предпросмотр администраторской загрузки без отправки файла на сервер.
document.querySelectorAll('.media-upload input[type="file"]').forEach(input=>input.addEventListener('change',()=>{
 const form=input.closest('.media-upload'), preview=form.querySelector('.media-upload-preview'), error=form.querySelector('.media-upload-error');
 const file=input.files?.[0], allowed=['image/png','image/jpeg','image/webp'];
 if(preview.dataset.objectUrl) URL.revokeObjectURL(preview.dataset.objectUrl);
 preview.hidden=true; error.hidden=true;
 if(!file)return;
 if(!allowed.includes(file.type)||file.size>5*1024*1024){error.textContent='Выберите PNG, JPEG или WebP размером не более 5 МБ.';error.hidden=false;input.value='';return;}
 const url=URL.createObjectURL(file);preview.dataset.objectUrl=url;preview.src=url;preview.hidden=false;
}));

// У личной программы «Открыть» показывает тот же редактор, поэтому рядом даём явное действие «Изменить».
document.querySelectorAll('.personal-program-row__actions').forEach(actions=>{
 if([...actions.querySelectorAll('a')].some(link=>link.textContent.trim()==='Изменить'))return;
 const open=[...actions.querySelectorAll('a')].find(link=>link.textContent.trim()==='Открыть');
 if(!open)return;
 const edit=open.cloneNode(true);edit.textContent='Изменить';open.after(edit);
});

document.querySelectorAll('.admin-image-field input[type="file"]').forEach(input=>{
  input.addEventListener('change',()=>{
    const file=input.files?.[0];
    if(!file)return;
    const holder=input.closest('.admin-image-field')?.querySelector('.catalog-placeholder');
    if(!holder)return;
    holder.querySelectorAll('img').forEach(img=>{img.src=URL.createObjectURL(file);img.hidden=false;});
  });
});
