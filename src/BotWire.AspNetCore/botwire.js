"use strict";(()=>{var m="/support",o=class extends Error{constructor(e,i,s){super(i);this.status=e;this.httpStatus=s;this.name="BotWireError"}},l=class{constructor(t={}){this._sessionToken=null;this.endpoint=x(t.endpoint??m),this.publicKey=t.publicKey;let e=t.fetch??globalThis.fetch;if(!e)throw new Error("BotWireClient: no global fetch available \u2014 pass config.fetch");this._fetch=e.bind(globalThis)}getSessionToken(){return this._sessionToken}setSessionToken(t){this._sessionToken=t}async initSession(t){let e=await this.post(`${this.endpoint}/session`,{},t);if(!e.ok)throw await this.toError(e);let i=await e.json();return this._sessionToken=i.sessionToken,{sessionToken:i.sessionToken,needsName:i.needsName??!1,errorMessage:i.errorMessage}}async chat(t,e={}){await this.ensureSession(e.signal);let i=await this.post(`${this.endpoint}/chat`,this.body(t,e),e.signal);await this.staleSession(i)&&(await this.initSession(e.signal),i=await this.post(`${this.endpoint}/chat`,this.body(t,e),e.signal));let s;try{s=await i.json()}catch{throw await this.toError(i)}return s.sessionToken&&(this._sessionToken=s.sessionToken),s}async*streamChat(t,e={}){await this.ensureSession(e.signal);let i=await this.post(`${this.endpoint}/chat/stream`,this.body(t,e),e.signal);if(await this.staleSession(i)&&(await this.initSession(e.signal),i=await this.post(`${this.endpoint}/chat/stream`,this.body(t,e),e.signal)),!i.ok||!i.body)throw await this.toError(i);yield*this.parseSse(i.body)}async ensureSession(t){this._sessionToken||await this.initSession(t)}body(t,e){let i={message:t,sessionToken:this._sessionToken};return e.contactEmail&&(i.contactEmail=e.contactEmail),i}async staleSession(t){if(t.status!==400)return!1;try{if((await t.clone().json()).status==="InvalidSession")return this._sessionToken=null,!0}catch{}return!1}async*parseSse(t){let e=t.getReader(),i=new TextDecoder,s="";try{for(;;){let{value:a,done:r}=await e.read();if(r)break;s+=i.decode(a,{stream:!0});let c;for(;(c=s.indexOf(`
`))!==-1;){let p=s.slice(0,c);if(s=s.slice(c+1),!p.startsWith("data: "))continue;let u=p.slice(6);if(u==="[DONE]"){yield{type:"done"};return}let g=v(u);g&&(yield g)}}}finally{e.releaseLock()}}post(t,e,i){let s={"Content-Type":"application/json"};return this.publicKey&&(s["X-BotWire-Key"]=this.publicKey),this._fetch(t,{method:"POST",headers:s,body:JSON.stringify(e),signal:i})}async toError(t){let e="Error",i=`BotWire request failed (HTTP ${t.status})`;try{let s=await t.clone().json();s.status&&(e=s.status),s.message&&(i=s.message)}catch{}return new o(e,i,t.status)}};function x(n){let t=n.length;for(;t>0&&n.charCodeAt(t-1)===47;)t--;return n.slice(0,t)}function v(n){let t;try{t=JSON.parse(n)}catch{return null}switch(t.type){case"token":return{type:"delta",delta:t.value??""};case"collect_contact":return{type:"collect_contact"};case"escalated":return{type:"escalated",ticketId:t.ticketId??"",message:t.message??""};case"blocked":return{type:"blocked",reason:t.reason??""};default:return null}}var d="botwire_session",f={en:{title:"Support",greeting:"How can we help you today?",placeholder:"Type a message\u2026",sendLabel:"Send",contactPrompt:"Please leave your email address so our team can follow up with you.",emailPlaceholder:"your@email.com",submitLabel:"Submit",cancelLabel:"Cancel",cancelMessage:"You have ended this conversation."},"zh-CN":{title:"\u5728\u7EBF\u5BA2\u670D",greeting:"\u8BF7\u95EE\u6709\u4EC0\u4E48\u53EF\u4EE5\u5E2E\u60A8\uFF1F",placeholder:"\u8F93\u5165\u6D88\u606F\u2026",sendLabel:"\u53D1\u9001",contactPrompt:"\u8BF7\u7559\u4E0B\u60A8\u7684\u90AE\u7BB1\uFF0C\u65B9\u4FBF\u6211\u4EEC\u7684\u56E2\u961F\u8DDF\u8FDB\u3002",emailPlaceholder:"your@email.com",submitLabel:"\u63D0\u4EA4",cancelLabel:"\u53D6\u6D88",cancelMessage:"\u60A8\u5DF2\u7ED3\u675F\u672C\u6B21\u4F1A\u8BDD\u3002"},ja:{title:"\u30B5\u30DD\u30FC\u30C8",greeting:"\u3054\u7528\u4EF6\u3092\u304A\u77E5\u3089\u305B\u304F\u3060\u3055\u3044\u3002",placeholder:"\u30E1\u30C3\u30BB\u30FC\u30B8\u3092\u5165\u529B\u2026",sendLabel:"\u9001\u4FE1",contactPrompt:"\u30E1\u30FC\u30EB\u30A2\u30C9\u30EC\u30B9\u3092\u3054\u8A18\u5165\u3044\u305F\u3060\u3051\u308C\u3070\u3001\u62C5\u5F53\u8005\u3088\u308A\u3054\u9023\u7D61\u3044\u305F\u3057\u307E\u3059\u3002",emailPlaceholder:"your@email.com",submitLabel:"\u9001\u4FE1\u3059\u308B",cancelLabel:"\u30AD\u30E3\u30F3\u30BB\u30EB",cancelMessage:"\u3053\u306E\u4F1A\u8A71\u3092\u7D42\u4E86\u3057\u307E\u3057\u305F\u3002"}};function y(n){if(!n)return"en";let t=n.toLowerCase();return t==="zh"||t.startsWith("zh-")||t.startsWith("zh_")?"zh-CN":t==="ja"||t.startsWith("ja-")||t.startsWith("ja_")?"ja":"en"}function w(n){return n.replace(/\\/g,"\\\\").replace(/'/g,"\\'")}function k(n,t,e){let s=t==="bottom-left"?"left":"right";return`
*,*::before,*::after{box-sizing:border-box;margin:0;padding:0}

#bubble{
  position:fixed;bottom:24px;${s}:24px;
  width:56px;height:56px;
  background:${n};border:none;border-radius:50%;cursor:pointer;
  display:flex;align-items:center;justify-content:center;
  box-shadow:0 4px 20px rgba(0,0,0,.28);
  transition:transform .2s ease,box-shadow .2s ease;
  z-index:2147483646;color:#fff;
}
#bubble:hover{transform:scale(1.08);box-shadow:0 6px 24px rgba(0,0,0,.32)}
#bubble svg{width:26px;height:26px;fill:#fff;pointer-events:none;flex-shrink:0}

#panel{
  position:fixed;bottom:96px;${s}:20px;
  width:360px;min-height:400px;max-height:560px;
  background:#fff;border-radius:16px;
  box-shadow:0 8px 48px rgba(0,0,0,.18);
  display:flex;flex-direction:column;overflow:hidden;
  z-index:2147483646;
  animation:bw-in .2s ease-out;
}
#panel[hidden]{display:none!important}
@keyframes bw-in{from{transform:scale(.92) translateY(8px);opacity:0}to{transform:none;opacity:1}}

#header{
  display:flex;align-items:center;justify-content:space-between;gap:8px;
  padding:14px 16px;background:${n};color:#fff;flex-shrink:0;
}
#header-title{font-weight:600;font-size:15px;flex:1}
#header-actions{display:flex;align-items:center;gap:2px}
#reset,#close{
  background:none;border:none;color:#fff;cursor:pointer;
  font-size:20px;line-height:1;padding:2px 6px;border-radius:6px;opacity:.75;
  display:flex;align-items:center;
}
#reset[hidden]{display:none!important}
#reset svg{width:17px;height:17px;fill:#fff}
#reset:hover,#close:hover{opacity:1;background:rgba(255,255,255,.15)}

#messages{
  flex:1;overflow-y:auto;padding:12px 12px 4px;
  display:flex;flex-direction:column;gap:8px;
  scroll-behavior:smooth;
}
#messages:empty::before{
  content:'${w(e)}';
  color:#94a3b8;font-size:13px;text-align:center;
  margin:auto;padding:32px 16px;
}

.msg{
  max-width:82%;padding:10px 14px;border-radius:14px;
  font-size:14px;line-height:1.55;word-break:break-word;
  animation:msg-in .15s ease-out;
}
@keyframes msg-in{from{transform:translateY(5px);opacity:0}to{transform:none;opacity:1}}
.msg-user{align-self:flex-end;background:${n};color:#fff;border-bottom-right-radius:3px}
.msg-bot {align-self:flex-start;background:#f1f5f9;color:#1e293b;border-bottom-left-radius:3px}
.msg-sys {
  align-self:center;background:#fef9c3;color:#854d0e;
  font-size:13px;border-radius:8px;text-align:center;max-width:90%;
}

#typing{padding:6px 12px 8px;display:flex;gap:4px;align-items:center;flex-shrink:0}
#typing[hidden]{display:none!important}
#typing span{
  width:7px;height:7px;background:#94a3b8;border-radius:50%;
  animation:bw-dot .9s infinite ease-in-out;
}
#typing span:nth-child(2){animation-delay:.15s}
#typing span:nth-child(3){animation-delay:.3s}
@keyframes bw-dot{0%,80%,100%{transform:scale(.6);opacity:.4}40%{transform:scale(1);opacity:1}}

#starters{
  display:flex;flex-wrap:wrap;gap:8px;
  padding:4px 12px 12px;flex-shrink:0;
}
#starters[hidden]{display:none!important}
.starter{
  background:#f1f5f9;color:#334155;border:1px solid #e2e8f0;border-radius:999px;
  padding:7px 14px;cursor:pointer;font:inherit;font-size:13px;
  transition:background .15s,border-color .15s;
}
.starter:hover{background:#e2e8f0;border-color:#cbd5e1}

#input-area{
  display:flex;gap:8px;align-items:flex-end;
  padding:10px 12px;border-top:1px solid #e2e8f0;flex-shrink:0;
}
#input-area[hidden]{display:none!important}
#input{
  flex:1;resize:none;border:1px solid #e2e8f0;border-radius:10px;
  padding:8px 12px;font:inherit;font-size:14px;outline:none;
  max-height:100px;overflow-y:auto;line-height:1.5;
  transition:border-color .15s;
}
#input:focus{border-color:${n};outline:none}
#send{
  background:${n};color:#fff;border:none;border-radius:10px;
  padding:9px 16px;cursor:pointer;font-size:14px;font-weight:500;
  white-space:nowrap;flex-shrink:0;transition:opacity .15s;
}
#send:disabled{opacity:.45;cursor:not-allowed}
#send:not(:disabled):hover{opacity:.88}

#contact-form{
  padding:14px 16px 16px;border-top:1px solid #e2e8f0;
  display:flex;flex-direction:column;gap:10px;flex-shrink:0;
}
#contact-form[hidden]{display:none!important}
#contact-form p{font-size:13px;color:#64748b;line-height:1.5}
#email-input{
  border:1px solid #e2e8f0;border-radius:10px;
  padding:9px 12px;font:inherit;font-size:14px;outline:none;
  transition:border-color .15s;
}
#email-input:focus{border-color:${n}}
#contact-buttons{display:flex;gap:8px}
#contact-submit{
  flex:1;background:${n};color:#fff;border:none;border-radius:10px;
  padding:10px;cursor:pointer;font-size:14px;font-weight:500;
  transition:opacity .15s;
}
#contact-submit:hover{opacity:.88}
#contact-cancel{
  flex:1;background:#f1f5f9;color:#64748b;border:none;border-radius:10px;
  padding:10px;cursor:pointer;font-size:14px;font-weight:500;
  transition:background .15s;
}
#contact-cancel:hover{background:#e2e8f0}

#ticket-card{
  margin:12px 12px 14px;padding:14px 16px;
  background:#f0fdf4;border:1px solid #bbf7d0;border-radius:12px;
  font-size:13px;color:#166534;text-align:center;line-height:1.55;
  flex-shrink:0;
}
#ticket-card[hidden]{display:none!important}

@media(max-width:480px){
  #panel{bottom:0;left:0;right:0;width:100%;min-height:unset;max-height:100%;
    border-radius:0;border-top-left-radius:16px;border-top-right-radius:16px}
  #bubble{bottom:16px;${s}:16px}
}
`}var b='<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path d="M20 2H4a2 2 0 00-2 2v18l4-4h14a2 2 0 002-2V4a2 2 0 00-2-2z"/></svg>',E='<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path d="M20 2H4a2 2 0 00-2 2v18l4-4h14a2 2 0 002-2V4a2 2 0 00-2-2zM6 10h12v2H6v-2zm0-4h12v2H6V6zm8 8H6v-2h8v2z"/></svg>',T='<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path d="M17.65 6.35A7.96 7.96 0 0012 4a8 8 0 108 8h-2a6 6 0 11-1.76-4.24L13 11h7V4l-2.35 2.35z"/></svg>',h=class extends HTMLElement{constructor(){super();this.streaming=!1;this.streamAbort=null;this.awaitingEmail=!1;this.ticketCreated=!1;this.errorOccurred=!1;this.errorMessage="Something went wrong. Please try again.";this.shadow=this.attachShadow({mode:"open"})}get endpoint(){return this.dataset.endpoint??"/support"}get primary(){return this.dataset.primaryColor??"#6366f1"}get position(){return this.dataset.position??"bottom-right"}get publicKey(){return this.dataset.publicKey}get offtopicMessage(){return this.dataset.offtopicMessage}get resetEnabled(){return this.dataset.reset!=="false"}get resetConfirm(){return this.dataset.resetConfirm!=="false"}get langKey(){return y(this.dataset.lang)}t(e){let i=this.dataset[e];return i!==void 0?i:f[this.langKey]?.[e]??f.en[e]}get widgetTitle(){return this.t("title")}get placeholder(){return this.t("placeholder")}get contactPrompt(){return this.t("contactPrompt")}get emailPlaceholder(){return this.t("emailPlaceholder")}get sendLabel(){return this.t("sendLabel")}get submitLabel(){return this.t("submitLabel")}get cancelLabel(){return this.t("cancelLabel")}get cancelMessage(){return this.t("cancelMessage")}get greeting(){return this.t("greeting")}get starters(){return(this.dataset.starters??"").split("|").map(e=>e.trim()).filter(e=>e.length>0)}connectedCallback(){this.mount(),this.client=new l({endpoint:this.endpoint,publicKey:this.publicKey}),this.client.setSessionToken(sessionStorage.getItem(d)),this.client.getSessionToken()||this.initSession()}mount(){this.shadow.innerHTML=`
<style>${k(this.primary,this.position,this.greeting)}</style>
<button id="bubble" aria-label="Open support chat" aria-expanded="false">${b}</button>
<div id="panel" hidden role="dialog" aria-label="${this.esc(this.widgetTitle)} support chat">
  <div id="header">
    <span id="header-title">${this.esc(this.widgetTitle)}</span>
    <div id="header-actions">
      <button id="reset" type="button" hidden aria-label="Reset conversation">${T}</button>
      <button id="close" aria-label="Close chat">\u2715</button>
    </div>
  </div>
  <div id="messages" role="log" aria-live="polite" aria-relevant="additions"></div>
  <div id="starters" hidden></div>
  <div id="typing" hidden aria-hidden="true"><span></span><span></span><span></span></div>
  <div id="input-area">
    <textarea id="input" placeholder="${this.esc(this.placeholder)}" rows="1" aria-label="Message input"></textarea>
    <button id="send" type="button">${this.esc(this.sendLabel)}</button>
  </div>
  <form id="contact-form" hidden>
    <p>${this.esc(this.contactPrompt)}</p>
    <input type="email" id="email-input" placeholder="${this.esc(this.emailPlaceholder)}" required aria-label="Email address">
    <div id="contact-buttons">
      <button id="contact-submit" type="submit">${this.esc(this.submitLabel)}</button>
      <button id="contact-cancel" type="button">${this.esc(this.cancelLabel)}</button>
    </div>
  </form>
  <div id="ticket-card" hidden role="status"></div>
</div>`,this.panel=this.q("#panel"),this.bubble=this.q("#bubble"),this.messages=this.q("#messages"),this.startersBox=this.q("#starters"),this.resetBtn=this.q("#reset"),this.typing=this.q("#typing"),this.inputArea=this.q("#input-area"),this.input=this.q("#input"),this.sendBtn=this.q("#send"),this.contact=this.q("#contact-form"),this.emailIn=this.q("#email-input"),this.cancelBtn=this.q("#contact-cancel"),this.ticket=this.q("#ticket-card"),this.bubble.addEventListener("click",()=>this.toggle()),this.q("#close").addEventListener("click",()=>this.close()),this.resetBtn.addEventListener("click",()=>this.handleReset()),this.sendBtn.addEventListener("click",()=>this.handleSend()),this.input.addEventListener("keydown",e=>{e.key==="Enter"&&!e.shiftKey&&(e.preventDefault(),this.handleSend())}),this.input.addEventListener("input",()=>this.autoResize()),this.contact.addEventListener("submit",e=>{e.preventDefault(),this.handleContactSubmit()}),this.cancelBtn.addEventListener("click",()=>this.handleContactCancel()),this.resetBtn.hidden=!this.resetEnabled,this.renderStarters()}async initSession(){try{let e=await this.client.initSession();sessionStorage.setItem(d,e.sessionToken),e.errorMessage&&(this.errorMessage=e.errorMessage)}catch{}}toggle(){this.panel.hidden?this.open():this.ticketCreated?(this.resetConversation(),this.input.focus()):this.close()}open(){this.ticketCreated&&this.resetConversation(),this.panel.hidden=!1,this.bubble.innerHTML=E,this.bubble.setAttribute("aria-expanded","true"),this.awaitingEmail?this.emailIn.focus():this.input.focus()}resetConversation(){this.streamAbort?.abort(),this.streamAbort=null,this.messages.innerHTML="",this.ticketCreated=!1,this.awaitingEmail=!1,this.streaming=!1,this.errorOccurred=!1,this.contact.hidden=!0,this.ticket.hidden=!0,this.inputArea.hidden=!1,this.sendBtn.disabled=!1,this.emailIn.value="",this.client.setSessionToken(null),sessionStorage.removeItem(d),this.renderStarters(),this.initSession()}handleReset(){if(this.resetConfirm){let e=this.dataset.resetConfirmMessage??"Start a new conversation?";if(typeof confirm=="function"&&!confirm(e))return}this.resetConversation(),!this.panel.hidden&&!this.awaitingEmail&&this.input.focus()}renderStarters(){this.startersBox.innerHTML="";let e=this.starters;if(e.length===0){this.startersBox.hidden=!0;return}for(let i of e){let s=document.createElement("button");s.type="button",s.className="starter",s.textContent=i,s.addEventListener("click",()=>{this.input.value=i,this.handleSend()}),this.startersBox.appendChild(s)}this.startersBox.hidden=!1}close(){this.errorOccurred&&this.resetConversation(),this.panel.hidden=!0,this.bubble.innerHTML=b,this.bubble.setAttribute("aria-expanded","false")}handleSend(){if(this.streaming||this.awaitingEmail)return;let e=this.input.value.trim();e&&(this.input.value="",this.autoResize(),this.startersBox.hidden=!0,this.appendMessage("user",e),this.stream(e))}handleContactSubmit(){let e=this.emailIn.value.trim();e&&(this.contact.hidden=!0,this.awaitingEmail=!1,this.stream("",e))}handleContactCancel(){this.contact.hidden=!0,this.awaitingEmail=!1,this.ticketCreated=!0,this.appendMessage("sys",this.cancelMessage)}async stream(e,i){if(this.streaming)return;this.streaming=!0,this.sendBtn.disabled=!0,this.typing.hidden=!1;let s=new AbortController;this.streamAbort=s;let a=null;try{for await(let r of this.client.streamChat(e,{contactEmail:i,signal:s.signal}))switch(this.typing.hidden=!0,r.type){case"delta":a||(a=this.appendMessage("bot","")),a.textContent+=r.delta,this.scrollBottom();break;case"collect_contact":this.inputArea.hidden=!0,this.contact.hidden=!1,this.awaitingEmail=!0,requestAnimationFrame(()=>{this.scrollBottom(),this.emailIn.focus()});break;case"escalated":this.ticketCreated=!0,this.ticket.hidden=!1,this.ticket.textContent=r.message,this.inputArea.hidden=!0;break;case"blocked":this.appendMessage("bot",this.offtopicMessage??r.reason);break;case"done":break}}catch(r){if(s.signal.aborted)return;this.typing.hidden=!0,r instanceof DOMException&&r.name==="AbortError"||(r instanceof o?(this.errorOccurred=!0,this.appendMessage("sys",this.errorMessage)):this.appendMessage("sys","Connection error. Please try again."))}finally{if(this.streamAbort===s){this.streamAbort=null;let r=this.client.getSessionToken();r&&sessionStorage.setItem(d,r),this.typing.hidden=!0,this.streaming=!1,!this.awaitingEmail&&!this.ticketCreated&&(this.sendBtn.disabled=!1,this.panel.hidden||this.input.focus())}}}appendMessage(e,i){let s=document.createElement("div");return s.className=`msg msg-${e}`,s.textContent=i,this.messages.appendChild(s),this.scrollBottom(),s}scrollBottom(){this.messages.scrollTop=this.messages.scrollHeight}autoResize(){this.input.style.height="auto",this.input.style.height=`${Math.min(this.input.scrollHeight,100)}px`}esc(e){return e.replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}q(e){return this.shadow.querySelector(e)}};customElements.define("botwire-widget",h);})();
